using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Pâtés de maisons : les bâtiments OSM qui se touchent sont regroupés, chaque pâté reçoit son propre trottoir
// (bordure, bateaux aux passages piétons) qui s'arrête au bord de la chaussée, puis ses façades Generic
// alignées sur le contour simplifié, et enfin un habillage aléatoire des rideaux de fer.
// Les contours sont calculés sur une grille (fermeture, bouchage des cours) : robuste aux géométries OSM bizarres.
partial class OsmImporter
{
    [System.Serializable]
    class BlockSettings
    {
        public bool Enabled = true;
        public float MaxSidewalk = 5f;
        public float MinSidewalk = 1.2f;
        public float CurbHeight = 0.18f;
        public bool RandomRamps = true;
        public bool Facades = true;
        public bool KeepOsmBuildings;
        public bool AutoHabillage = true;
        public float ShopChance = 0.45f, GraffChance = 0.35f;
        public int Seed = 1;
    }

    static void BlockSettingsGUI(BlockSettings s)
    {
        s.Enabled = EditorGUILayout.ToggleLeft("Pâtés de maisons + trottoirs", s.Enabled, EditorStyles.boldLabel);
        if (!s.Enabled)
            return;
        EditorGUI.indentLevel++;
        s.MaxSidewalk = Mathf.Max(1f, EditorGUILayout.FloatField(new GUIContent("Trottoir max (m)", "Largeur quand aucune route n'arrête le trottoir"), s.MaxSidewalk));
        s.MinSidewalk = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Trottoir min (m)", "Garanti même si la route OSM mord sur le bâtiment"), s.MinSidewalk), 0.3f, s.MaxSidewalk);
        s.CurbHeight = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Hauteur bordure (m)", "Le vélo monte une marche de 0,2 m"), s.CurbHeight), 0.05f, 0.4f);
        s.RandomRamps = EditorGUILayout.Toggle(new GUIContent("Bateaux aléatoires", "En plus de ceux des passages piétons"), s.RandomRamps);
        s.Facades = EditorGUILayout.Toggle("Façades Generic", s.Facades);
        using (new EditorGUI.DisabledScope(!s.Facades))
        {
            s.KeepOsmBuildings = EditorGUILayout.Toggle(new GUIContent("Garder les blocs OSM", "Blocs blancs d'origine derrière les façades"), s.KeepOsmBuildings);
            s.AutoHabillage = EditorGUILayout.Toggle("Habiller les rideaux", s.AutoHabillage);
            using (new EditorGUI.DisabledScope(!s.AutoHabillage))
            {
                s.ShopChance = EditorGUILayout.Slider("Chance shop", s.ShopChance, 0f, 1f);
                s.GraffChance = EditorGUILayout.Slider("Chance graff", s.GraffChance, 0f, 1f - s.ShopChance);
            }
        }
        s.Seed = EditorGUILayout.IntField(new GUIContent("Seed", "Même seed = même ville"), s.Seed);
        EditorGUI.indentLevel--;
    }

    class RoadSurface
    {
        public List<(List<Vector2> points, float width)> Lines = new List<(List<Vector2>, float)>();
        public List<(Vector2 center, float radius)> Discs = new List<(Vector2, float)>();
        public List<Area> Areas = new List<Area>();
    }

    const string FacadesDir = "Assets/Prefabs/Batiments";
    const string ObstacleTag = "Obstacle";
    const float RasterCell = 0.25f;
    // Bâtiments à moins de cette distance = même pâté
    const float JoinDistance = 1f;
    // Fermeture du masque : bouche les fentes entre bâtiments d'un même pâté
    const float CloseRadius = 0.75f;
    const float CurbWidth = 0.25f, CurbChamfer = 0.03f;
    // Bateau creusé dans le trottoir : partie basse, pentes latérales, profondeur de la pente vers l'intérieur
    const float RampWidth = 2.4f, RampTransition = 1f, RampDepth = 1.3f, RampLip = 0.02f;
    // Largeur de trottoir mesurée tous les SampleStep mètres le long de chaque façade
    const float SampleStep = 1f;
    const string ConcreteTexture = "Assets/Prefabs/FBX_Blender/Packed/textures/gravel_embedded_concrete_diff_1k.jpg";
    // Écart à l'angle droit sous lequel deux façades sont raccourcies pour ne pas se chevaucher (z-fighting)
    const float CornerTolerance = 6f, CornerGap = 0.03f;
    const float MinFacadeRun = 2.5f;
    // Déformation en largeur tolérée avant pénalité forte
    const float MaxStretch = 1.2f;

    partial class SceneBuilder
    {
        // Kiosques et petits abris restent de simples blocs : ils ne forment pas de pâté
        static bool BlockCandidate(Area area) =>
            Tag(area.Tags, "building") is not ("kiosk" or "roof" or "shed" or "hut" or "carport") && Mathf.Abs(SignedArea(area.Outer)) >= 20f;

        class Block
        {
            public List<Area> Buildings = new List<Area>();
            public List<Vector2> Footprint, Sidewalk;
            public Vector2 Pivot;
        }

        void BuildBlocks(Transform root, string name, Material buildingMaterial)
        {
            var random = new System.Random(Blocks.Seed);
            var groups = GroupBuildings(buildingAreas);
            var crossings = CrossingPoints();
            var facades = Blocks.Facades ? LoadFacades() : new List<(GameObject prefab, FacadeGeneric info)>();
            var shops = Habillage.Shops();
            var graffs = Habillage.Graffs();

            var blocksRoot = Child(root, "Pâtés").transform;
            // Béton des îlots : dalle plus foncée, bordure plus grise
            var materials = new[] { ConcreteMaterial("Trottoir", new Color(0.42f, 0.42f, 0.41f)), ConcreteMaterial("Bordure", new Color(0.68f, 0.68f, 0.66f)) };
            var roofMaterial = GetMaterial("Toit", new Color(0.18f, 0.18f, 0.20f));
            float roadLevel = Height(Ground.Route), top = roadLevel + Blocks.CurbHeight;

            // Sol roulable au niveau de la chaussée : sans lui le vélo roule sur le sol de la scène et la bordure
            // n'a pas la bonne hauteur
            if (!AddColliders)
            {
                Vector2 min = Project(data.MinLat, data.MinLon), max = Project(data.MaxLat, data.MaxLon);
                var floor = Child(root, "Sol collision");
                floor.layer = LayerMask.NameToLayer("Floor");
                var box = floor.AddComponent<BoxCollider>();
                box.center = V((min + max) / 2, roadLevel - 0.5f);
                box.size = new Vector3(max.x - min.x, 1f, max.y - min.y);
            }

            // Index spatial des bâtiments pour retrouver les voisins (limite de trottoir entre deux pâtés)
            var index = new SpatialIndex(25f);
            for (int i = 0; i < buildingAreas.Count; i++)
                index.Add(i, Bounds2(buildingAreas[i].Outer, 0f));
            var groupOf = new int[buildingAreas.Count];
            for (int g = 0; g < groups.Count; g++)
                foreach (int i in groups[g])
                    groupOf[i] = g;

            for (int g = 0; g < groups.Count; g++)
            {
                EditorUtility.DisplayProgressBar("Import OSM", $"Pâtés ({g + 1}/{groups.Count})", 0.6f + 0.3f * g / groups.Count);
                var block = new Block { Buildings = groups[g].Select(i => buildingAreas[i]).ToList() };
                if (!ComputeOutlines(block, groups[g], index, groupOf, g))
                {
                    foreach (var area in block.Buildings)
                        CreateBuilding(area, blocksRoot, buildingMaterial);
                    continue;
                }

                var go = Child(blocksRoot, $"Pâté {g + 1}");
                go.transform.localPosition = V(block.Pivot, 0f);
                var footprint = block.Footprint.Select(p => p - block.Pivot).ToList();
                var sidewalk = block.Sidewalk.Select(p => p - block.Pivot).ToList();

                var ramps = PlaceRamps(sidewalk, crossings.Select(p => p - block.Pivot).ToList(), Blocks, random);
                var walk = Child(go.transform, "Trottoir");
                var mesh = SidewalkMesh(sidewalk, ramps, roadLevel, top, $"{name} Trottoir {g + 1}");
                meshes.Add(mesh);
                walk.AddComponent<MeshFilter>().sharedMesh = mesh;
                walk.AddComponent<MeshRenderer>().sharedMaterials = materials;
                walk.AddComponent<MeshCollider>().sharedMesh = mesh;
                // Comme les trottoirs de CarrefourCity : marche (StepUp) à basse vitesse, crash au-delà
                walk.tag = ObstacleTag;
                walk.layer = LayerMask.NameToLayer("Floor");

                bool dressed = facades.Count > 0 && PlaceFacades(go.transform, footprint, p => HeightAt(block, p + block.Pivot), facades, top, Blocks, random, shops, graffs);
                if (dressed)
                {
                    // Plafond sombre sous les toits des façades : cache le vide intérieur du pâté
                    var roof = Child(go.transform, "Toit");
                    float roofHeight = top + facades.Min(f => f.info.height) - 2f;
                    // En retrait du bord pour rester caché derrière le haut des façades
                    var inset = Offset(footprint, 2f);
                    var roofMesh = FlatMesh(SignedArea(inset) > 0f ? inset : footprint, roofHeight, $"{name} Toit {g + 1}");
                    meshes.Add(roofMesh);
                    roof.AddComponent<MeshFilter>().sharedMesh = roofMesh;
                    roof.AddComponent<MeshRenderer>().sharedMaterial = roofMaterial;
                }
                if (!dressed || Blocks.KeepOsmBuildings)
                {
                    var osm = Child(go.transform, "Batiments OSM").transform;
                    osm.localPosition = -V(block.Pivot, 0f);
                    foreach (var area in block.Buildings)
                        CreateBuilding(area, osm, buildingMaterial, top);
                }
            }
        }

        // --- Regroupement ---

        static (Vector2 min, Vector2 max) Bounds2(List<Vector2> ring, float margin)
        {
            Vector2 min = ring[0], max = ring[0];
            foreach (var p in ring)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return (min - Vector2.one * margin, max + Vector2.one * margin);
        }

        static List<List<int>> GroupBuildings(List<Area> buildings)
        {
            var parent = Enumerable.Range(0, buildings.Count).ToArray();
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);

            var index = new SpatialIndex(25f);
            var bounds = buildings.Select(b => Bounds2(b.Outer, JoinDistance)).ToList();
            for (int i = 0; i < buildings.Count; i++)
                index.Add(i, bounds[i]);
            for (int i = 0; i < buildings.Count; i++)
                foreach (int j in index.Query(bounds[i]))
                {
                    if (j <= i || Find(i) == Find(j))
                        continue;
                    if (PolygonDistance(buildings[i].Outer, buildings[j].Outer) <= JoinDistance)
                        parent[Find(i)] = Find(j);
                }
            return Enumerable.Range(0, buildings.Count).GroupBy(Find).Select(g => g.ToList()).ToList();
        }

        static float PolygonDistance(List<Vector2> a, List<Vector2> b)
        {
            if (PointInPolygon(a[0], b) || PointInPolygon(b[0], a))
                return 0f;
            float best = float.MaxValue;
            foreach (var p in a)
                best = Mathf.Min(best, DistanceToRing(p, b));
            foreach (var p in b)
                best = Mathf.Min(best, DistanceToRing(p, a));
            return best;
        }

        static float DistanceToRing(Vector2 p, List<Vector2> ring)
        {
            float best = float.MaxValue;
            for (int i = 0; i < ring.Count; i++)
                best = Mathf.Min(best, (ClosestOnSegment(p, ring[i], ring[(i + 1) % ring.Count]) - p).magnitude);
            return best;
        }

        // Passages piétons : nœuds highway=crossing et extrémités des ways footway=crossing
        List<Vector2> CrossingPoints()
        {
            var points = new List<Vector2>();
            foreach (var kv in data.NodeTags)
            {
                if ((Tag(kv.Value, "highway") == "crossing" || Tag(kv.Value, "crossing") != null) && data.Nodes.TryGetValue(kv.Key, out var n))
                    points.Add(Project(n.lat, n.lon));
            }
            foreach (var way in data.Ways)
            {
                if (Tag(way.Tags, "footway") != "crossing" && !(Tag(way.Tags, "crossing") != null && Tag(way.Tags, "highway") is "footway" or "path" or "cycleway"))
                    continue;
                foreach (var id in new[] { way.Nodes[0], way.Nodes[way.Nodes.Count - 1] })
                    if (data.Nodes.TryGetValue(id, out var n))
                        points.Add(Project(n.lat, n.lon));
            }
            return points;
        }

        // --- Contours sur grille ---

        bool ComputeOutlines(Block block, List<int> members, SpatialIndex index, int[] groupOf, int group)
        {
            var (min, max) = Bounds2(block.Buildings.SelectMany(b => b.Outer).ToList(), Blocks.MaxSidewalk + 2f);
            var grid = new Raster(min, max, RasterCell);

            var own = grid.New();
            foreach (var b in block.Buildings)
                grid.FillPolygon(own, b.Outer);
            var footprint = grid.FillHoles(grid.Erode(grid.Dilate(own, CloseRadius), CloseRadius));
            for (int i = 0; i < own.Length; i++)
                footprint[i] |= own[i];

            // Bâtiments des autres pâtés et chaussée : limites du trottoir
            var others = grid.New();
            foreach (int i in index.Query((min, max)))
                if (groupOf[i] != group)
                    grid.FillPolygon(others, buildingAreas[i].Outer);
            var road = grid.New();
            foreach (var (points, width) in roads.Lines)
                for (int i = 0; i < points.Count - 1; i++)
                    grid.FillCapsule(road, points[i], points[i + 1], width / 2);
            foreach (var (center, radius) in roads.Discs)
                grid.FillCapsule(road, center, center, radius);
            foreach (var area in roads.Areas)
                grid.FillPolygon(road, area.Outer);

            var outline = grid.Outline(footprint);
            if (outline == null)
                return false;
            block.Footprint = CleanRing(Simplify(outline, 0.6f), 1.5f, 12f);
            if (block.Footprint.Count < 3)
                return false;

            // Ligne de rue lissée (sans les décrochés entre bâtiments voisins) : la bordure reste droite
            var street = CleanRing(Simplify(outline, 1f), 3f, 15f);
            if (street.Count < 3)
                street = block.Footprint;
            var widths = SidewalkWidths(street, grid, road, others);
            KeepSidewalkInFront(street, block.Footprint, widths);
            block.Sidewalk = OffsetOutline(street, widths) ?? RasterOutline(street, widths, grid, footprint);
            if (block.Sidewalk == null || block.Sidewalk.Count < 3)
                return false;
            block.Pivot = block.Footprint.Aggregate(Vector2.zero, (s, p) => s + p) / block.Footprint.Count;
            return true;
        }

        // Largeur du trottoir devant chaque façade : distance jusqu'à la chaussée OSM (ou mi-distance jusqu'au pâté voisin),
        // médiane des mesures le long du côté, puis même largeur pour les côtés presque alignés
        List<float> SidewalkWidths(List<Vector2> ring, Raster grid, bool[] road, bool[] others)
        {
            int n = ring.Count;
            var widths = new float[n];
            var lengths = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % n];
                lengths[i] = (b - a).magnitude;
                var dir = (b - a) / lengths[i];
                var outward = new Vector2(dir.y, -dir.x);
                int count = Mathf.Max(3, Mathf.CeilToInt(lengths[i] / SampleStep));
                var samples = new List<float>();
                for (int k = 0; k < count; k++)
                {
                    var p = a + dir * (lengths[i] * (k + 0.5f) / count);
                    samples.Add(MeasureSidewalk(p, outward, grid, road, others));
                }
                samples.Sort();
                widths[i] = samples[samples.Count / 2];
            }

            // Groupes de côtés presque alignés (rue droite découpée en plusieurs bâtiments) : médiane pondérée
            var smoothed = new List<float>(widths);
            for (int i = 0; i < n; i++)
            {
                var group = new List<int> { i };
                for (int k = i; group.Count < n && Turn(ring, k + 1) < 20f; k++)
                    group.Add((k + 1) % n);
                for (int k = i; group.Count < n && Turn(ring, k) < 20f; k--)
                    group.Add(((k - 1) % n + n) % n);
                var sorted = group.Distinct().OrderBy(g => widths[g]).ToList();
                float half = sorted.Sum(g => lengths[g]) / 2, cumul = 0f;
                foreach (int g in sorted)
                {
                    cumul += lengths[g];
                    if (cumul >= half)
                    {
                        smoothed[i] = widths[g];
                        break;
                    }
                }
            }
            return smoothed;
        }

        // Une façade qui dépasse de la ligne lissée garde au moins le trottoir minimum devant elle
        void KeepSidewalkInFront(List<Vector2> street, List<Vector2> footprint, List<float> widths)
        {
            int n = street.Count;
            var overhang = new float[n];
            foreach (var p in footprint)
            {
                int nearest = 0;
                float best = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float d = (ClosestOnSegment(p, street[i], street[(i + 1) % n]) - p).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        nearest = i;
                    }
                }
                Vector2 a = street[nearest], dir = (street[(nearest + 1) % n] - a).normalized;
                overhang[nearest] = Mathf.Max(overhang[nearest], Vector2.Dot(p - a, new Vector2(dir.y, -dir.x)));
            }
            for (int i = 0; i < n; i++)
                widths[i] = Mathf.Max(widths[i], overhang[i] + Blocks.MinSidewalk);
        }

        // Angle de virage (degrés) au sommet i
        static float Turn(List<Vector2> ring, int i)
        {
            int n = ring.Count;
            Vector2 a = ring[((i - 1) % n + n) % n], b = ring[(i % n + n) % n], c = ring[(i + 1) % n];
            return Vector2.Angle(b - a, c - b);
        }

        float MeasureSidewalk(Vector2 p, Vector2 outward, Raster grid, bool[] road, bool[] others)
        {
            const float step = 0.1f;
            for (float d = step; d <= Blocks.MaxSidewalk * 2f; d += step)
            {
                int k = grid.Index(p + outward * d);
                if (k < 0)
                    break;
                if (road[k])
                    return Mathf.Clamp(d, Blocks.MinSidewalk, Blocks.MaxSidewalk);
                if (others[k])
                    return Mathf.Clamp(d / 2, Blocks.MinSidewalk, Blocks.MaxSidewalk);
            }
            // Ni route ni voisin (place, cour ouverte) : largeur par défaut de l'import
            return Mathf.Clamp(SidewalkWidth, Blocks.MinSidewalk, Blocks.MaxSidewalk);
        }

        // Contour décalé de chaque façade de sa largeur, angles saillants arrondis, boucles parasites retirées.
        // null si le résultat reste invalide (repli sur la grille)
        static List<Vector2> OffsetOutline(List<Vector2> ring, List<float> widths)
        {
            int n = ring.Count;
            var result = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                int prev = (i + n - 1) % n;
                Vector2 v = ring[i];
                Vector2 d0 = (v - ring[prev]).normalized, d1 = (ring[(i + 1) % n] - v).normalized;
                Vector2 n0 = new Vector2(d0.y, -d0.x), n1 = new Vector2(d1.y, -d1.x);
                float w0 = widths[prev], w1 = widths[i];
                if (Cross(d0, d1) > 1e-4f)
                {
                    // Angle saillant : arc autour du coin du bâtiment, rayon qui passe d'une largeur à l'autre
                    float a0 = Mathf.Atan2(n0.y, n0.x), a1 = Mathf.Atan2(n1.y, n1.x);
                    // Parcours anti-horaire : la normale extérieure tourne dans le sens positif
                    while (a1 < a0)
                        a1 += 2 * Mathf.PI;
                    int steps = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / (15f * Mathf.Deg2Rad)));
                    for (int k = 0; k <= steps; k++)
                    {
                        float t = (float)k / steps, angle = Mathf.Lerp(a0, a1, t);
                        result.Add(v + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Lerp(w0, w1, t));
                    }
                }
                else
                {
                    // Angle rentrant : intersection des deux bords décalés
                    Vector2 p0 = v + n0 * w0, p1 = v + n1 * w1;
                    float denom = Cross(d0, d1);
                    if (Mathf.Abs(denom) < 1e-3f)
                        result.Add((p0 + p1) / 2);
                    else
                        result.Add(p0 + d0 * (Cross(p1 - p0, d1) / denom));
                }
            }
            result = RemoveLoops(result);
            if (result == null || result.Count < 3 || SignedArea(result) <= SignedArea(ring) || SelfIntersects(result))
                return null;
            return CleanRing(result, 0.05f, 1f);
        }

        // Retire les petites boucles créées quand le décalage est plus large qu'un renfoncement de façade
        static List<Vector2> RemoveLoops(List<Vector2> ring)
        {
            var r = new List<Vector2>(ring);
            for (int guard = 0; guard < 200; guard++)
            {
                bool found = false;
                for (int i = 0; i < r.Count && !found; i++)
                    for (int j = i + 2; j < r.Count && !found; j++)
                    {
                        if (i == 0 && j == r.Count - 1)
                            continue;
                        Vector2 a = r[i], b = r[(i + 1) % r.Count], c = r[j], d = r[(j + 1) % r.Count];
                        if (!SegmentIntersection(a, b, c, d, out var x))
                            continue;
                        // Garde le plus grand des deux morceaux
                        int inner = j - i, outer = r.Count - inner;
                        if (inner <= outer)
                        {
                            r.RemoveRange(i + 1, inner);
                            r.Insert(i + 1, x);
                        }
                        else
                        {
                            r = r.Skip(i + 1).Take(inner).ToList();
                            r.Insert(0, x);
                        }
                        found = true;
                    }
                if (!found)
                    return r;
            }
            return null;
        }

        static bool SegmentIntersection(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 x)
        {
            x = default;
            Vector2 r = b - a, s = d - c;
            float denom = Cross(r, s);
            if (Mathf.Abs(denom) < 1e-8f)
                return false;
            float t = Cross(c - a, s) / denom, u = Cross(c - a, r) / denom;
            if (t <= 1e-4f || t >= 1 - 1e-4f || u <= 1e-4f || u >= 1 - 1e-4f)
                return false;
            x = a + r * t;
            return true;
        }

        static bool SelfIntersects(List<Vector2> ring)
        {
            for (int i = 0; i < ring.Count; i++)
                for (int j = i + 2; j < ring.Count; j++)
                {
                    if (i == 0 && j == ring.Count - 1)
                        continue;
                    if (SegmentIntersection(ring[i], ring[(i + 1) % ring.Count], ring[j], ring[(j + 1) % ring.Count], out _))
                        return true;
                }
            return false;
        }

        // Repli : mêmes bandes parallèles aux façades, réunies sur la grille
        static List<Vector2> RasterOutline(List<Vector2> ring, List<float> widths, Raster grid, bool[] footprint)
        {
            var walk = (bool[])footprint.Clone();
            int n = ring.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = ring[i], b = ring[(i + 1) % n], d = (b - a).normalized, o = new Vector2(d.y, -d.x);
                grid.FillPolygon(walk, new List<Vector2> { a, b, b + o * widths[i], a + o * widths[i] });
                grid.FillCapsule(walk, b, b, Mathf.Min(widths[i], widths[(i + 1) % n]));
            }
            var outline = grid.Outline(grid.FillHoles(walk));
            return outline == null ? null : CleanRing(Simplify(outline, 0.2f), 0.3f, 3f);
        }

        // --- Trottoir ---

        // Bateau : côté du contour du trottoir et distance le long de ce côté
        static List<(int edge, float along)> PlaceRamps(List<Vector2> ring, List<Vector2> crossings, BlockSettings settings, System.Random random)
        {
            var ramps = new List<(int edge, float along)>();
            const float margin = RampWidth / 2 + RampTransition + 0.3f;
            Vector2 Position((int edge, float along) r) => ring[r.edge] + (ring[(r.edge + 1) % ring.Count] - ring[r.edge]).normalized * r.along;
            bool TryAdd(int edge, float along)
            {
                float length = (ring[(edge + 1) % ring.Count] - ring[edge]).magnitude;
                if (length < 2 * margin)
                    return false;
                var ramp = (edge, Mathf.Clamp(along, margin, length - margin));
                var p = Position(ramp);
                if (ramps.Any(r => (Position(r) - p).sqrMagnitude < 25f))
                    return false;
                ramps.Add(ramp);
                return true;
            }

            // Passage piéton : point le plus proche sur un côté assez long pour accueillir le bateau
            foreach (var c in crossings)
            {
                int bestEdge = -1;
                float bestDistance = 8f, bestAlong = 0f;
                for (int i = 0; i < ring.Count; i++)
                {
                    Vector2 a = ring[i], b = ring[(i + 1) % ring.Count];
                    float length = (b - a).magnitude;
                    if (length < 2 * margin)
                        continue;
                    var dir = (b - a) / length;
                    float along = Mathf.Clamp(Vector2.Dot(c - a, dir), margin, length - margin);
                    float d = (a + dir * along - c).magnitude;
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        bestEdge = i;
                        bestAlong = along;
                    }
                }
                if (bestEdge >= 0)
                    TryAdd(bestEdge, bestAlong);
            }

            if (settings.RandomRamps)
            {
                // Entrées de garage et bateaux isolés : environ un tous les 40 m sur les grands côtés
                for (int i = 0; i < ring.Count; i++)
                {
                    float length = (ring[(i + 1) % ring.Count] - ring[i]).magnitude;
                    if (length < 10f)
                        continue;
                    for (int k = 0; k < Mathf.CeilToInt(length / 40f); k++)
                        if (random.NextDouble() < 0.5)
                            TryAdd(i, (float)random.NextDouble() * length);
                }
            }
            return ramps;
        }

        // Dalle du trottoir avec bordure chanfreinée (sous-mesh 1). Le long du bord, une bande de RampDepth
        // relie la bordure au dessus de la dalle : plate en temps normal, en pente là où la bordure s'abaisse
        // pour un bateau. Pentes ~9-11° : normales assez verticales pour que PlayerCollision les prenne pour du sol
        static Mesh SidewalkMesh(List<Vector2> outer, List<(int edge, float along)> ramps, float roadLevel, float top, string name)
        {
            var sb = new SurfaceBuilder(2);
            var chamfer = Offset(outer, CurbChamfer);
            var curb = Offset(outer, CurbWidth);
            var inner = Offset(outer, RampDepth);
            float low = roadLevel + RampLip;
            float perimeter = 0f;
            int n = outer.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float length = (outer[j] - outer[i]).magnitude;
                var d = outer[j] - outer[i];
                var outward = new Vector3(d.y, 0f, -d.x).normalized;

                Vector2 P(List<Vector2> ring, float s) => Vector2.Lerp(ring[i], ring[j], s / length);
                var normal2 = new Vector2(outward.x, outward.z);
                // Bande assez profonde sur toute la largeur du bateau (près d'un angle rentrant elle se resserre
                // et la pente deviendrait un mur pour PlayerCollision)
                bool Fits(float along) => new[] { -1f, -0.5f, 0f, 0.5f, 1f }
                    .Select(k => along + k * (RampWidth / 2 + RampTransition))
                    .All(st => Vector2.Dot(P(outer, st) - P(inner, st), normal2) > RampDepth * 0.95f);

                // Stations le long du côté avec la hauteur de la bordure
                var stations = new List<(float s, float h)> { (0f, top), (length, top) };
                foreach (var (edge, along) in ramps.Where(r => r.edge == i && Fits(r.along)))
                {
                    stations.Add((along - RampWidth / 2 - RampTransition, top));
                    stations.Add((along - RampWidth / 2, low));
                    stations.Add((along + RampWidth / 2, low));
                    stations.Add((along + RampWidth / 2 + RampTransition, top));
                }
                stations = stations.OrderBy(st => st.s).ToList();
                for (int k = 0; k + 1 < stations.Count; k++)
                {
                    var (sa, ha) = stations[k];
                    var (sb2, hb) = stations[k + 1];
                    if (sb2 - sa < 1e-3f)
                        continue;
                    Vector2 oa = P(outer, sa), ob = P(outer, sb2), ca = P(chamfer, sa), cb = P(chamfer, sb2);
                    Vector2 ka = P(curb, sa), kb = P(curb, sb2), ia = P(inner, sa), ib = P(inner, sb2);
                    float ua = (perimeter + sa) * 0.5f, ub = (perimeter + sb2) * 0.5f;

                    // Face verticale de la bordure, depuis sous la chaussée
                    sb.Quad(1, V(oa, 0f), V(ob, 0f), V(ob, hb - CurbChamfer), V(oa, ha - CurbChamfer),
                        new Vector2(ua, 0f), new Vector2(ub, 0f), new Vector2(ub, hb), new Vector2(ua, ha), outward);
                    // Chanfrein puis dessus de la bordure
                    sb.Quad(1, V(oa, ha - CurbChamfer), V(ob, hb - CurbChamfer), V(cb, hb), V(ca, ha),
                        new Vector2(ua, ha), new Vector2(ub, hb), new Vector2(ub, hb + 0.03f), new Vector2(ua, ha + 0.03f), outward + Vector3.up);
                    sb.Quad(1, V(ca, ha), V(cb, hb), V(kb, hb), V(ka, ha),
                        new Vector2(ua, 0.1f), new Vector2(ub, 0.1f), new Vector2(ub, 0.25f), new Vector2(ua, 0.25f), Vector3.up);
                    // Bande dalle : de la bordure jusqu'au dessus du trottoir (pente des bateaux)
                    sb.GentleQuad(0, V(ka, ha), V(kb, hb), V(ib, top), V(ia, top), ka * 0.5f, kb * 0.5f, ib * 0.5f, ia * 0.5f);
                }
                perimeter += length;
            }

            var (polygon, triangles) = Triangulate(inner, new List<List<Vector2>>());
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = polygon[triangles[i]], b = polygon[triangles[i + 1]], c = polygon[triangles[i + 2]];
                sb.Tri(0, V(a, top), V(b, top), V(c, top), a * 0.5f, b * 0.5f, c * 0.5f, Vector3.up);
            }
            return sb.ToMesh(name);
        }

        // Béton texturé comme les îlots ; un matériau déjà texturé n'est pas modifié (retouches à la main conservées)
        static Material ConcreteMaterial(string name, Color color)
        {
            var material = GetMaterial(name, color);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ConcreteTexture);
            if (material.GetTexture("_BaseMap") == null && texture != null)
            {
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", 0.1f);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        static Mesh FlatMesh(List<Vector2> ring, float y, string name)
        {
            var sb = new SurfaceBuilder(1);
            var (polygon, triangles) = Triangulate(ring, new List<List<Vector2>>());
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = polygon[triangles[i]], b = polygon[triangles[i + 1]], c = polygon[triangles[i + 2]];
                sb.Tri(0, V(a, y), V(b, y), V(c, y), a, b, c, Vector3.up);
            }
            return sb.ToMesh(name);
        }

        // Décalage vers l'intérieur d'un anneau anti-horaire, onglets limités dans les angles aigus
        static List<Vector2> Offset(List<Vector2> ring, float distance)
        {
            var result = new List<Vector2>(ring.Count);
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 prev = ring[(i + ring.Count - 1) % ring.Count], p = ring[i], next = ring[(i + 1) % ring.Count];
                var n1 = Left(p - prev);
                var n2 = Left(next - p);
                var miter = n1 + n2;
                miter = miter.sqrMagnitude < 1e-6f ? n1 : miter.normalized;
                result.Add(p + miter * (distance / Mathf.Max(Vector2.Dot(miter, n1), 0.35f)));
            }
            return result;
        }

        // --- Façades ---

        static List<(GameObject prefab, FacadeGeneric info)> LoadFacades() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { FacadesDir }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(p => p != null && p.GetComponent<FacadeGeneric>() != null)
                .Select(p => (p, p.GetComponent<FacadeGeneric>())).ToList();

        // ring : contour anti-horaire dans l'espace de blockRoot ; heightAt : hauteur visée en un point de cet espace
        static bool PlaceFacades(Transform blockRoot, List<Vector2> ring, System.Func<Vector2, float> heightAt, List<(GameObject prefab, FacadeGeneric info)> facades,
            float top, BlockSettings settings, System.Random random, List<GameObject> shops, List<Texture2D> graffs)
        {
            var parent = Child(blockRoot, "Façades").transform;
            int placed = 0;
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 prev = ring[(i + ring.Count - 1) % ring.Count], a = ring[i], b = ring[(i + 1) % ring.Count], next = ring[(i + 2) % ring.Count];
                float length = (b - a).magnitude;
                var dir = (b - a) / length;
                var outward = new Vector2(dir.y, -dir.x);

                // Angles droits saillants : les façades s'arrêtent juste avant pour que les côtés ne se superposent pas
                float start = NearRightCorner(prev, a, b) ? CornerGap : 0f;
                float end = length - (NearRightCorner(a, b, next) ? CornerGap : 0f);
                float run = end - start;
                if (run < MinFacadeRun)
                    continue;

                // Hauteur visée : bâtiment juste derrière la façade
                float TargetHeight(float along) => heightAt(a + dir * (start + Mathf.Clamp(along, 0f, run)) - outward * 1.5f);
                var (sequence, scale) = ChooseFacades(run, TargetHeight, facades, random);

                float cursor = start;
                foreach (int f in sequence)
                {
                    var (prefab, info) = facades[f];
                    float width = info.width * scale;
                    var center = a + dir * (cursor + width / 2);
                    cursor += width;

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    go.transform.localPosition = V(center, top);
                    go.transform.localRotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y));
                    go.transform.localScale = new Vector3(scale, 1f, 1f);
                    placed++;

                    if (settings.AutoHabillage)
                        foreach (var rideau in go.GetComponentsInChildren<RideauDeFer>())
                            Habillage.Randomize(rideau, random, settings.ShopChance, settings.GraffChance, shops, graffs);
                }
            }
            return placed > 0;
        }

        // Angle intérieur saillant proche de 90° au sommet b
        static bool NearRightCorner(Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 d1 = (b - a).normalized, d2 = (c - b).normalized;
            if (Cross(d1, d2) <= 0f)
                return false;
            float interior = 180f - Vector2.Angle(d1, d2);
            return Mathf.Abs(interior - 90f) < CornerTolerance;
        }

        float HeightAt(Block block, Vector2 point)
        {
            Area best = block.Buildings.FirstOrDefault(b => PointInPolygon(point, b.Outer));
            if (best == null)
                best = block.Buildings.OrderBy(b => DistanceToRing(point, b.Outer)).First();
            return BuildingHeights(best.Tags).top;
        }

        // Suite de façades qui remplit la longueur avec le moins d'étirement possible, en évitant les répétitions
        // et en prenant des hauteurs proches des bâtiments OSM. Recherche aléatoire, meilleur préfixe gardé.
        static (List<int> sequence, float scale) ChooseFacades(float run, System.Func<float, float> targetHeight,
            List<(GameObject prefab, FacadeGeneric info)> facades, System.Random random)
        {
            float minWidth = facades.Min(f => f.info.width);
            if (run < minWidth / MaxStretch)
            {
                // Côté court : une seule façade étirée, la plus proche en largeur et en hauteur
                float h = targetHeight(run / 2);
                var weights = facades.Select(f => Mathf.Exp(-Mathf.Abs(f.info.height - h) / 4f - 3f * Mathf.Abs(Mathf.Log(run / f.info.width)))).ToList();
                int pick = Pick(random, weights);
                return (new List<int> { pick }, run / facades[pick].info.width);
            }

            List<int> best = null;
            float bestCost = float.MaxValue, bestScale = 1f;
            for (int trial = 0; trial < 150; trial++)
            {
                var sequence = new List<int>();
                float width = 0f, heightError = 0f;
                int repeats = 0;
                while (width < run)
                {
                    float h = targetHeight(width + minWidth / 2);
                    int previous = sequence.Count > 0 ? sequence[sequence.Count - 1] : -1;
                    var weights = facades.Select((f, k) => Mathf.Exp(-Mathf.Abs(f.info.height - h) / 4f) * (k == previous ? 0.3f : 1f)).ToList();
                    int pick = Pick(random, weights);
                    if (pick == previous)
                        repeats++;
                    sequence.Add(pick);
                    heightError += Mathf.Abs(facades[pick].info.height - h);
                    width += facades[pick].info.width;

                    // Chaque préfixe qui couvre au moins 70 % de la longueur est candidat
                    if (width < run * 0.7f)
                        continue;
                    float scale = run / width;
                    float stretch = Mathf.Abs(Mathf.Log(scale));
                    float cost = 10f * stretch + (stretch > Mathf.Log(MaxStretch) ? 5f : 0f)
                                 + 0.1f * heightError / sequence.Count + 0.5f * repeats;
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = new List<int>(sequence);
                        bestScale = scale;
                    }
                }
            }
            return (best, bestScale);
        }

        static int Pick(System.Random random, List<float> weights)
        {
            double r = random.NextDouble() * weights.Sum();
            for (int i = 0; i < weights.Count; i++)
            {
                r -= weights[i];
                if (r <= 0)
                    return i;
            }
            return weights.Count - 1;
        }
    }

    static Vector2 ClosestOnSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var d = b - a;
        float t = d.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude);
        return a + d * t;
    }

    // --- Simplification de contours ---

    // Douglas-Peucker sur anneau fermé
    static List<Vector2> Simplify(List<Vector2> ring, float tolerance)
    {
        if (ring.Count < 4)
            return ring;
        // Coupe l'anneau entre le premier point et le point le plus éloigné
        int far = 0;
        for (int i = 1; i < ring.Count; i++)
            if ((ring[i] - ring[0]).sqrMagnitude > (ring[far] - ring[0]).sqrMagnitude)
                far = i;
        var keep = new bool[ring.Count];
        keep[0] = keep[far] = true;
        var closed = new List<Vector2>(ring) { ring[0] };
        SimplifyRange(closed, 0, far, tolerance, keep);
        SimplifyRange(closed, far, ring.Count, tolerance, keep);
        return Enumerable.Range(0, ring.Count).Where(i => keep[i]).Select(i => ring[i]).ToList();
    }

    static void SimplifyRange(List<Vector2> points, int first, int last, float tolerance, bool[] keep)
    {
        if (last - first < 2)
            return;
        float max = 0f;
        int index = -1;
        for (int i = first + 1; i < last; i++)
        {
            float d = DistanceToLine(points[i], points[first], points[last]);
            if (d > max)
            {
                max = d;
                index = i;
            }
        }
        if (max <= tolerance)
            return;
        keep[index] = true;
        SimplifyRange(points, first, index, tolerance, keep);
        SimplifyRange(points, index, last, tolerance, keep);
    }

    static float DistanceToLine(Vector2 p, Vector2 a, Vector2 b)
    {
        var d = b - a;
        return d.sqrMagnitude < 1e-8f ? (p - a).magnitude : Mathf.Abs(Cross(d, p - a)) / d.magnitude;
    }

    // Retire les sommets presque alignés et les côtés trop courts (le sommet dont le retrait change le moins l'aire)
    static List<Vector2> CleanRing(List<Vector2> ring, float minEdge, float minAngle)
    {
        var r = new List<Vector2>(ring);
        bool changed = true;
        while (changed && r.Count > 3)
        {
            changed = false;
            for (int i = 0; i < r.Count && r.Count > 3; i++)
            {
                Vector2 a = r[(i + r.Count - 1) % r.Count], b = r[i], c = r[(i + 1) % r.Count];
                if (Vector2.Angle(b - a, c - b) < minAngle)
                {
                    r.RemoveAt(i);
                    changed = true;
                    i--;
                }
            }
            int shortest = -1;
            float shortestLength = minEdge;
            for (int i = 0; i < r.Count; i++)
            {
                float length = (r[(i + 1) % r.Count] - r[i]).magnitude;
                if (length < shortestLength)
                {
                    shortestLength = length;
                    shortest = i;
                }
            }
            if (shortest >= 0 && r.Count > 3)
            {
                int i = shortest, j = (shortest + 1) % r.Count;
                r.RemoveAt(TriangleArea(r, i) < TriangleArea(r, j) ? i : j);
                changed = true;
            }
        }
        return r;
    }

    static float TriangleArea(List<Vector2> r, int i) =>
        Mathf.Abs(Cross(r[i] - r[(i + r.Count - 1) % r.Count], r[(i + 1) % r.Count] - r[i])) / 2;

    // --- Grille ---

    class SpatialIndex
    {
        readonly float cell;
        readonly Dictionary<(int, int), List<int>> cells = new Dictionary<(int, int), List<int>>();

        public SpatialIndex(float cell) => this.cell = cell;

        IEnumerable<(int, int)> Cells((Vector2 min, Vector2 max) b)
        {
            for (int x = Mathf.FloorToInt(b.min.x / cell); x <= Mathf.FloorToInt(b.max.x / cell); x++)
                for (int y = Mathf.FloorToInt(b.min.y / cell); y <= Mathf.FloorToInt(b.max.y / cell); y++)
                    yield return (x, y);
        }

        public void Add(int item, (Vector2 min, Vector2 max) b)
        {
            foreach (var c in Cells(b))
                (cells.TryGetValue(c, out var list) ? list : cells[c] = new List<int>()).Add(item);
        }

        public IEnumerable<int> Query((Vector2 min, Vector2 max) b) =>
            Cells(b).SelectMany(c => cells.TryGetValue(c, out var list) ? list : Enumerable.Empty<int>()).Distinct();
    }

    // Masque binaire sur une grille régulière. Les distances sont en mètres
    class Raster
    {
        public readonly int W, H;
        readonly float cell;
        readonly Vector2 origin;

        public Raster(Vector2 min, Vector2 max, float cell)
        {
            this.cell = cell;
            origin = min;
            W = Mathf.CeilToInt((max.x - min.x) / cell) + 1;
            H = Mathf.CeilToInt((max.y - min.y) / cell) + 1;
        }

        public bool[] New() => new bool[W * H];

        // Index de la cellule qui contient p, -1 hors de la grille
        public int Index(Vector2 p)
        {
            int i = Mathf.FloorToInt((p.x - origin.x) / cell), j = Mathf.FloorToInt((p.y - origin.y) / cell);
            return i < 0 || j < 0 || i >= W || j >= H ? -1 : j * W + i;
        }

        // Remplissage pair-impair des cellules dont le centre est dans le polygone
        public void FillPolygon(bool[] mask, List<Vector2> ring)
        {
            var xs = new List<float>();
            for (int j = 0; j < H; j++)
            {
                float y = origin.y + (j + 0.5f) * cell;
                xs.Clear();
                for (int k = 0; k < ring.Count; k++)
                {
                    Vector2 a = ring[k], b = ring[(k + 1) % ring.Count];
                    if ((a.y > y) != (b.y > y))
                        xs.Add(a.x + (y - a.y) * (b.x - a.x) / (b.y - a.y));
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int i0 = Mathf.Max(0, Mathf.CeilToInt((xs[k] - origin.x) / cell - 0.5f));
                    int i1 = Mathf.Min(W - 1, Mathf.FloorToInt((xs[k + 1] - origin.x) / cell - 0.5f));
                    for (int i = i0; i <= i1; i++)
                        mask[j * W + i] = true;
                }
            }
        }

        // Cellules à moins de radius du segment a-b
        public void FillCapsule(bool[] mask, Vector2 a, Vector2 b, float radius)
        {
            var min = Vector2.Min(a, b) - Vector2.one * radius - origin;
            var max = Vector2.Max(a, b) + Vector2.one * radius - origin;
            int i0 = Mathf.Max(0, (int)(min.x / cell)), i1 = Mathf.Min(W - 1, (int)(max.x / cell));
            int j0 = Mathf.Max(0, (int)(min.y / cell)), j1 = Mathf.Min(H - 1, (int)(max.y / cell));
            float r2 = radius * radius;
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    var p = origin + new Vector2((i + 0.5f) * cell, (j + 0.5f) * cell);
                    if ((ClosestOnSegment(p, a, b) - p).sqrMagnitude <= r2)
                        mask[j * W + i] = true;
                }
        }

        // Distance de chanfrein (1, √2) à la cellule vraie la plus proche
        public float[] Distance(bool[] mask)
        {
            var d = new float[mask.Length];
            const float big = 1e9f, diagonal = 1.41421356f;
            for (int k = 0; k < d.Length; k++)
                d[k] = mask[k] ? 0f : big;
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    int k = j * W + i;
                    if (i > 0) d[k] = Mathf.Min(d[k], d[k - 1] + 1);
                    if (j > 0)
                    {
                        d[k] = Mathf.Min(d[k], d[k - W] + 1);
                        if (i > 0) d[k] = Mathf.Min(d[k], d[k - W - 1] + diagonal);
                        if (i < W - 1) d[k] = Mathf.Min(d[k], d[k - W + 1] + diagonal);
                    }
                }
            for (int j = H - 1; j >= 0; j--)
                for (int i = W - 1; i >= 0; i--)
                {
                    int k = j * W + i;
                    if (i < W - 1) d[k] = Mathf.Min(d[k], d[k + 1] + 1);
                    if (j < H - 1)
                    {
                        d[k] = Mathf.Min(d[k], d[k + W] + 1);
                        if (i < W - 1) d[k] = Mathf.Min(d[k], d[k + W + 1] + diagonal);
                        if (i > 0) d[k] = Mathf.Min(d[k], d[k + W - 1] + diagonal);
                    }
                }
            for (int k = 0; k < d.Length; k++)
                d[k] *= cell;
            return d;
        }

        public bool[] Dilate(bool[] mask, float radius)
        {
            var d = Distance(mask);
            return d.Select(v => v <= radius).ToArray();
        }

        public bool[] Erode(bool[] mask, float radius)
        {
            var d = Distance(mask.Select(v => !v).ToArray());
            return d.Select(v => v > radius).ToArray();
        }

        // Cours intérieures et trous bouchés : tout ce qui n'est pas relié au bord de la grille
        public bool[] FillHoles(bool[] mask)
        {
            var outside = new bool[mask.Length];
            var stack = new Stack<int>();
            for (int i = 0; i < W; i++)
            {
                stack.Push(i);
                stack.Push((H - 1) * W + i);
            }
            for (int j = 0; j < H; j++)
            {
                stack.Push(j * W);
                stack.Push(j * W + W - 1);
            }
            while (stack.Count > 0)
            {
                int k = stack.Pop();
                if (mask[k] || outside[k])
                    continue;
                outside[k] = true;
                int i = k % W, j = k / W;
                if (i > 0) stack.Push(k - 1);
                if (i < W - 1) stack.Push(k + 1);
                if (j > 0) stack.Push(k - W);
                if (j < H - 1) stack.Push(k + W);
            }
            return outside.Select(o => !o).ToArray();
        }

        // Cellules de region reliées (4-connexité) à une cellule de seeds
        public bool[] ConnectedTo(bool[] region, bool[] seeds)
        {
            var result = new bool[region.Length];
            var stack = new Stack<int>();
            for (int k = 0; k < seeds.Length; k++)
                if (seeds[k])
                {
                    int i = k % W, j = k / W;
                    if (i > 0) stack.Push(k - 1);
                    if (i < W - 1) stack.Push(k + 1);
                    if (j > 0) stack.Push(k - W);
                    if (j < H - 1) stack.Push(k + W);
                }
            while (stack.Count > 0)
            {
                int k = stack.Pop();
                if (!region[k] || result[k])
                    continue;
                result[k] = true;
                int i = k % W, j = k / W;
                if (i > 0) stack.Push(k - 1);
                if (i < W - 1) stack.Push(k + 1);
                if (j > 0) stack.Push(k - W);
                if (j < H - 1) stack.Push(k + W);
            }
            return result;
        }

        // Contour extérieur (anti-horaire) de la plus grande zone, en marches d'escalier sur les bords des cellules
        public List<Vector2> Outline(bool[] mask)
        {
            bool Filled(int i, int j) => i >= 0 && j >= 0 && i < W && j < H && mask[j * W + i];
            // Arêtes orientées avec la zone pleine à gauche, indexées par leur sommet de départ
            var edges = new Dictionary<int, List<int>>();
            int Vertex(int i, int j) => j * (W + 1) + i;
            void Edge(int from, int to) => (edges.TryGetValue(from, out var l) ? l : edges[from] = new List<int>(1)).Add(to);
            for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                {
                    if (!mask[j * W + i])
                        continue;
                    if (!Filled(i, j - 1)) Edge(Vertex(i, j), Vertex(i + 1, j));
                    if (!Filled(i + 1, j)) Edge(Vertex(i + 1, j), Vertex(i + 1, j + 1));
                    if (!Filled(i, j + 1)) Edge(Vertex(i + 1, j + 1), Vertex(i, j + 1));
                    if (!Filled(i - 1, j)) Edge(Vertex(i, j + 1), Vertex(i, j));
                }

            List<Vector2> best = null;
            float bestArea = 0f;
            while (edges.Count > 0)
            {
                int start = edges.Keys.First();
                var loop = new List<int>();
                int current = start, previous = -1;
                while (edges.TryGetValue(current, out var outgoing))
                {
                    // Sommet partagé en diagonale : on tourne à droite pour ne pas fusionner deux zones
                    int next = outgoing[0];
                    if (outgoing.Count > 1 && previous >= 0)
                    {
                        int dx = current % (W + 1) - previous % (W + 1), dy = current / (W + 1) - previous / (W + 1);
                        next = outgoing.OrderBy(o =>
                        {
                            int ox = o % (W + 1) - current % (W + 1), oy = o / (W + 1) - current / (W + 1);
                            return dx * oy - dy * ox; // < 0 = à droite
                        }).First();
                    }
                    outgoing.Remove(next);
                    if (outgoing.Count == 0)
                        edges.Remove(current);
                    loop.Add(current);
                    previous = current;
                    current = next;
                    if (current == start)
                        break;
                }
                var ring = loop.Select(v => origin + new Vector2(v % (W + 1), v / (W + 1)) * cell).ToList();
                // Supprime les sommets intermédiaires des segments droits
                ring = ring.Where((p, k) =>
                {
                    Vector2 a = ring[(k + ring.Count - 1) % ring.Count], c = ring[(k + 1) % ring.Count];
                    return Mathf.Abs(Cross(p - a, c - p)) > 1e-6f;
                }).ToList();
                if (ring.Count < 3)
                    continue;
                float area = SignedArea(ring);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = ring;
                }
            }
            return best;
        }
    }

    // Mesh à normales plates avec UV et plusieurs sous-meshes (un par matériau)
    class SurfaceBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] triangles;

        public SurfaceBuilder(int submeshes) => triangles = Enumerable.Range(0, submeshes).Select(_ => new List<int>()).ToArray();

        public void Tri(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0)
            {
                (b, c) = (c, b);
                (ub, uc) = (uc, ub);
            }
            int i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            uvs.Add(ua);
            uvs.Add(ub);
            uvs.Add(uc);
            triangles[submesh].Add(i);
            triangles[submesh].Add(i + 1);
            triangles[submesh].Add(i + 2);
        }

        public void Quad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 normal)
        {
            Tri(submesh, a, b, c, ua, ub, uc, normal);
            Tri(submesh, a, c, d, ua, uc, ud, normal);
        }

        // Quad gauchi coupé selon la diagonale qui donne la pente la plus douce (transitions des bateaux)
        public void GentleQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            float Up(Vector3 p, Vector3 q, Vector3 r)
            {
                var n = Vector3.Cross(q - p, r - p);
                return n.sqrMagnitude < 1e-12f ? 1f : Mathf.Abs(n.normalized.y);
            }
            if (Mathf.Min(Up(a, b, c), Up(a, c, d)) >= Mathf.Min(Up(a, b, d), Up(b, c, d)))
            {
                Tri(submesh, a, b, c, ua, ub, uc, Vector3.up);
                Tri(submesh, a, c, d, ua, uc, ud, Vector3.up);
            }
            else
            {
                Tri(submesh, a, b, d, ua, ub, ud, Vector3.up);
                Tri(submesh, b, c, d, ub, uc, ud, Vector3.up);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++)
                mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
