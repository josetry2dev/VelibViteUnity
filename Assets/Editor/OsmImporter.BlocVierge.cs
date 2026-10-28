using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Habille un bloc vierge modélisé à la main (cube, ProBuilder...) avec les façades Generic, comme un pâté de l'import OSM.
// Le contour est lu sur les faces du dessus du mesh, la hauteur du bloc guide le choix des façades.
partial class OsmImporter
{
    class HabillageBlocVierge : EditorWindow
    {
        const string DressedSuffix = " (habillé)";

        readonly BlockSettings settings = new BlockSettings();
        bool hideBlock = true;
        bool sidewalk = true;
        float sidewalkWidth = 3f;

        [MenuItem("FOUVELIB/Bâtiments/Habiller un bloc vierge...")]
        static void Open() => GetWindow<HabillageBlocVierge>(true, "Habiller un bloc vierge");

        void OnEnable() => settings.Seed = Random.Range(1, 100000);
        void OnSelectionChange() => Repaint();

        void OnGUI()
        {
            var targets = Targets();
            EditorGUILayout.HelpBox(targets.Count == 0
                ? "Sélectionne un ou plusieurs blocs (mesh posé au sol). Les façades suivent le contour du dessus du bloc."
                : $"{targets.Count} bloc(s) : {string.Join(", ", targets.Select(t => t.name))}", targets.Count == 0 ? MessageType.Info : MessageType.None);

            settings.AutoHabillage = EditorGUILayout.Toggle("Habiller les rideaux", settings.AutoHabillage);
            using (new EditorGUI.DisabledScope(!settings.AutoHabillage))
            {
                settings.ShopChance = EditorGUILayout.Slider("Chance shop", settings.ShopChance, 0f, 1f);
                settings.GraffChance = EditorGUILayout.Slider("Chance graff", settings.GraffChance, 0f, 1f - settings.ShopChance);
            }
            sidewalk = EditorGUILayout.Toggle("Trottoir", sidewalk);
            using (new EditorGUI.DisabledScope(!sidewalk))
            {
                EditorGUI.indentLevel++;
                sidewalkWidth = Mathf.Clamp(EditorGUILayout.FloatField("Largeur (m)", sidewalkWidth), 0.5f, 15f);
                settings.CurbHeight = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Hauteur bordure (m)", "Le vélo monte une marche de 0,2 m"), settings.CurbHeight), 0.05f, 0.4f);
                settings.RandomRamps = EditorGUILayout.Toggle("Bateaux aléatoires", settings.RandomRamps);
                EditorGUI.indentLevel--;
            }
            hideBlock = EditorGUILayout.Toggle(new GUIContent("Cacher le bloc vierge", "Désactivé (pas supprimé) une fois habillé"), hideBlock);
            EditorGUILayout.BeginHorizontal();
            settings.Seed = EditorGUILayout.IntField("Seed", settings.Seed);
            if (GUILayout.Button("Au hasard", GUILayout.Width(80)))
                settings.Seed = Random.Range(1, 100000);
            EditorGUILayout.EndHorizontal();

            using (new EditorGUI.DisabledScope(targets.Count == 0))
                if (GUILayout.Button(targets.Any(t => Dressed(t) != null) ? "Rhabiller la sélection" : "Habiller la sélection"))
                    Dress(targets);
        }

        // Blocs sélectionnés ; un habillage sélectionné renvoie à son bloc
        static List<GameObject> Targets() => Selection.gameObjects
            .Select(g => g.name.EndsWith(DressedSuffix) ? Block(g) ?? g : g)
            .Where(g => g.GetComponentInChildren<MeshFilter>() != null && !g.name.EndsWith(DressedSuffix))
            .Distinct().ToList();

        static GameObject Block(GameObject dressed)
        {
            var name = dressed.name.Substring(0, dressed.name.Length - DressedSuffix.Length);
            var parent = dressed.transform.parent;
            var siblings = parent != null ? parent.Cast<Transform>().Select(t => t.gameObject) : dressed.scene.GetRootGameObjects();
            return siblings.FirstOrDefault(g => g.name == name);
        }

        static GameObject Dressed(GameObject block)
        {
            var parent = block.transform.parent;
            var siblings = parent != null ? parent.Cast<Transform>().Select(t => t.gameObject) : block.scene.GetRootGameObjects();
            return siblings.FirstOrDefault(g => g.name == block.name + DressedSuffix);
        }

        void Dress(List<GameObject> targets)
        {
            var facades = SceneBuilder.Facades();
            if (facades.Count == 0)
            {
                EditorUtility.DisplayDialog("Habiller un bloc", $"Aucun prefab avec FacadeGeneric dans {FacadesDir}.", "OK");
                return;
            }
            Undo.SetCurrentGroupName("Habiller un bloc vierge");
            int group = Undo.GetCurrentGroup();
            var random = new System.Random(settings.Seed);
            var created = new List<Object>();
            foreach (var block in targets)
            {
                // Rhabillage : l'ancien habillage est remplacé, le bloc réaffiché le temps de relire sa forme
                var old = Dressed(block);
                if (old != null)
                    Undo.DestroyObjectImmediate(old);
                bool wasActive = block.activeSelf;
                block.SetActive(true);
                var result = SceneBuilder.DressBlock(block, facades, settings, sidewalk ? sidewalkWidth : 0f, random);
                block.SetActive(wasActive);
                if (result == null)
                {
                    Debug.LogWarning($"{block.name} : contour du dessus introuvable (il faut des faces horizontales vers le haut)", block);
                    continue;
                }
                result.name = block.name + DressedSuffix;
                Undo.RegisterCreatedObjectUndo(result, "Habiller un bloc vierge");
                if (hideBlock)
                {
                    Undo.RecordObject(block, "Habiller un bloc vierge");
                    block.SetActive(false);
                }
                created.Add(result);
            }
            Undo.CollapseUndoOperations(group);
            if (created.Count > 0)
                Selection.objects = created.ToArray();
        }
    }

    partial class SceneBuilder
    {
        public static List<(GameObject prefab, FacadeGeneric info)> Facades() => LoadFacades();

        // Habillage créé à côté du bloc (pas en enfant : le bloc est souvent un cube mis à l'échelle)
        // sidewalkWidth 0 = pas de trottoir. Le bas du bloc est le niveau de la chaussée
        public static GameObject DressBlock(GameObject block, List<(GameObject prefab, FacadeGeneric info)> facades, BlockSettings settings, float sidewalkWidth, System.Random random)
        {
            var filters = block.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).ToList();
            var ring = TopOutline(filters);
            if (ring == null)
                return null;

            var bounds = filters.Select(f => f.GetComponent<Renderer>()).Where(r => r != null).Select(r => r.bounds)
                .Aggregate((a, b) => { a.Encapsulate(b); return a; });
            float ground = bounds.min.y, height = bounds.size.y;

            var pivot = ring.Aggregate(Vector2.zero, (s, p) => s + p) / ring.Count;
            var local = ring.Select(p => p - pivot).ToList();
            var root = new GameObject();
            root.transform.SetParent(block.transform.parent, false);
            root.transform.position = new Vector3(pivot.x, ground, pivot.y);
            root.transform.SetSiblingIndex(block.transform.GetSiblingIndex() + 1);

            float top = 0f;
            if (sidewalkWidth > 0f)
            {
                top = settings.CurbHeight;
                var walk = Child(root.transform, "Trottoir");
                var outline = SidewalkOutline(local, sidewalkWidth);
                var mesh = SidewalkMesh(outline, PlaceRamps(outline, new List<Vector2>(), settings, random), 0f, top, block.name + " Trottoir");
                walk.AddComponent<MeshFilter>().sharedMesh = mesh;
                walk.AddComponent<MeshRenderer>().sharedMaterials = new[] { ConcreteMaterial("Trottoir", new Color(0.42f, 0.42f, 0.41f)), ConcreteMaterial("Bordure", new Color(0.68f, 0.68f, 0.66f)) };
                walk.AddComponent<MeshCollider>().sharedMesh = mesh;
                // Comme les trottoirs de CarrefourCity : marche (StepUp) à basse vitesse, crash au-delà
                walk.tag = ObstacleTag;
                walk.layer = LayerMask.NameToLayer("Floor");
            }

            if (!PlaceFacades(root.transform, local, _ => height, facades, top, settings, random, Habillage.Shops(), Habillage.Graffs()))
            {
                Object.DestroyImmediate(root);
                return null;
            }

            // Plafond sombre en retrait : cache le vide derrière les façades, comme pour les pâtés OSM
            var inset = Offset(local, 2f);
            var roof = Child(root.transform, "Toit");
            roof.AddComponent<MeshFilter>().sharedMesh = FlatMesh(SignedArea(inset) > 0f ? inset : local, top + facades.Min(f => f.info.height) - 2f, block.name + " Toit");
            roof.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial("Toit", new Color(0.18f, 0.18f, 0.20f));
            return root;
        }

        // Trottoir de largeur constante autour du contour, angles arrondis (repli sur grille si le décalage se recoupe)
        static List<Vector2> SidewalkOutline(List<Vector2> ring, float width)
        {
            var widths = ring.Select(_ => width).ToList();
            var outline = OffsetOutline(ring, widths);
            if (outline != null)
                return outline;
            var (min, max) = Bounds2(ring, width + 2f);
            var grid = new Raster(min, max, RasterCell);
            var footprint = grid.New();
            grid.FillPolygon(footprint, ring);
            return RasterOutline(ring, widths, grid, footprint);
        }

        // Contour anti-horaire (x, z) des faces tournées vers le haut : arêtes qui ne bordent qu'une seule de ces faces,
        // recollées en boucles, la plus grande gardée
        static List<Vector2> TopOutline(List<MeshFilter> filters)
        {
            var edges = new Dictionary<(Vector2Int, Vector2Int), int>();
            var positions = new Dictionary<Vector2Int, Vector2>();
            Vector2Int Key(Vector3 p)
            {
                var k = new Vector2Int(Mathf.RoundToInt(p.x * 1000f), Mathf.RoundToInt(p.z * 1000f));
                positions[k] = new Vector2(p.x, p.z);
                return k;
            }

            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                var matrix = filter.transform.localToWorldMatrix;
                var v = mesh.vertices.Select(p => matrix.MultiplyPoint3x4(p)).ToArray();
                var t = mesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    var normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < 1e-10f || normal.normalized.y < 0.7f)
                        continue;
                    var ka = Key(a);
                    var kb = Key(b);
                    var kc = Key(c);
                    foreach (var (p, q) in new[] { (ka, kb), (kb, kc), (kc, ka) })
                    {
                        if (p == q)
                            continue;
                        // Arête partagée par deux faces du dessus = intérieure : les deux sens s'annulent
                        if (edges.ContainsKey((q, p)))
                            edges.Remove((q, p));
                        else
                            edges[(p, q)] = 1;
                    }
                }
            }
            if (edges.Count < 3)
                return null;

            var next = new Dictionary<Vector2Int, List<Vector2Int>>();
            foreach (var (p, q) in edges.Keys)
                (next.TryGetValue(p, out var l) ? l : next[p] = new List<Vector2Int>()).Add(q);

            List<Vector2> best = null;
            float bestArea = 0f;
            while (next.Count > 0)
            {
                var start = next.Keys.First();
                var loop = new List<Vector2>();
                var current = start;
                while (next.TryGetValue(current, out var outgoing))
                {
                    var to = outgoing[0];
                    outgoing.RemoveAt(0);
                    if (outgoing.Count == 0)
                        next.Remove(current);
                    loop.Add(positions[current]);
                    current = to;
                    if (current == start)
                        break;
                }
                if (loop.Count < 3)
                    continue;
                float area = Mathf.Abs(SignedArea(loop));
                if (area > bestArea)
                {
                    bestArea = area;
                    best = loop;
                }
            }
            if (best == null)
                return null;
            // Les faces sont vues du dessus dans le repère (x, z) de Unity, qui inverse le sens : on force l'anti-horaire
            if (SignedArea(best) < 0)
                best.Reverse();
            return CleanRing(best, 0.5f, 3f);
        }
    }
}
