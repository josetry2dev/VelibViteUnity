using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEngine;

// Importe un fichier OpenStreetMap (.osm) en 3D à l'échelle, sans texture : un bloc par bâtiment
// et un sol plat coloré par type (routes, trottoirs, zones piétonnes, verdure...) pour reconstruire la map par-dessus.
// Axes : x = est, z = nord, 1 unité = 1 m. L'origine (lat, lon) tombe en (0, 0, 0).
class OsmImporter : EditorWindow
{
    const string OutputDir = "Assets/OSM";
    const string MaterialDir = OutputDir + "/Materials";
    const string LastFileKey = "FOUVELIB.OsmImporter.LastFile";

    // Ordre d'empilement du sol : chaque type est 2 cm au-dessus du précédent pour éviter le z-fighting
    enum Ground { Sol, Vegetation, Eau, Parking, Pieton, Trottoir, Route, Cyclable, Rail }
    const float GroundStep = 0.02f;

    static readonly Dictionary<Ground, Color> GroundColors = new Dictionary<Ground, Color>
    {
        { Ground.Sol, new Color(0.62f, 0.60f, 0.55f) },
        { Ground.Vegetation, new Color(0.45f, 0.65f, 0.35f) },
        { Ground.Eau, new Color(0.35f, 0.55f, 0.80f) },
        { Ground.Parking, new Color(0.50f, 0.50f, 0.53f) },
        { Ground.Pieton, new Color(0.86f, 0.80f, 0.62f) },
        { Ground.Trottoir, new Color(0.82f, 0.82f, 0.82f) },
        { Ground.Route, new Color(0.28f, 0.28f, 0.30f) },
        { Ground.Cyclable, new Color(0.70f, 0.35f, 0.30f) },
        { Ground.Rail, new Color(0.45f, 0.35f, 0.30f) },
    };
    static readonly Color BuildingColor = new Color(0.95f, 0.95f, 0.95f);

    string filePath = "";
    OsmData data;
    double originLat, originLon;
    float levelHeight = 3f;
    int defaultLevels = 5;
    float sidewalkWidth = 3f;
    bool addColliders;

    [MenuItem("FOUVELIB/Importer un fichier OSM...")]
    static void Open() => GetWindow<OsmImporter>(true, "Import OpenStreetMap");

    void OnEnable() => filePath = EditorPrefs.GetString(LastFileKey, "");

    void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.TextField("Fichier .osm", filePath);
        if (GUILayout.Button("Parcourir...", GUILayout.Width(90)))
        {
            var dir = File.Exists(filePath) ? Path.GetDirectoryName(filePath) : "";
            var picked = EditorUtility.OpenFilePanelWithFilters("Fichier OpenStreetMap", dir, new[] { "OpenStreetMap", "osm,xml", "Tous", "*" });
            if (!string.IsNullOrEmpty(picked))
                LoadFile(picked);
        }
        EditorGUILayout.EndHorizontal();

        if (data == null)
        {
            if (File.Exists(filePath) && GUILayout.Button("Charger"))
                LoadFile(filePath);
            EditorGUILayout.HelpBox("Export depuis openstreetmap.org (bouton Exporter) ou Overpass, au format XML .osm (pas .pbf).", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox($"{data.Nodes.Count} points, {data.Ways.Count} ways, {data.Relations.Count} relations — {data.BuildingCount} bâtiments", MessageType.None);

        EditorGUILayout.Space();
        originLat = EditorGUILayout.DoubleField(new GUIContent("Origine latitude", "Point GPS placé en (0, 0, 0)"), originLat);
        originLon = EditorGUILayout.DoubleField("Origine longitude", originLon);
        if (GUILayout.Button("Centrer sur le fichier"))
            CenterOrigin();
        EditorGUILayout.HelpBox("Garde la même origine pour importer des zones voisines : elles se raccorderont.", MessageType.None);

        EditorGUILayout.Space();
        levelHeight = Mathf.Max(1f, EditorGUILayout.FloatField(new GUIContent("Hauteur d'étage (m)", "Utilisée quand le bâtiment n'a que building:levels"), levelHeight));
        defaultLevels = Mathf.Max(1, EditorGUILayout.IntField(new GUIContent("Étages par défaut", "Bâtiments sans hauteur ni nombre d'étages"), defaultLevels));
        sidewalkWidth = Mathf.Max(0.5f, EditorGUILayout.FloatField(new GUIContent("Largeur trottoir (m)", "Trottoirs sans largeur renseignée"), sidewalkWidth));
        addColliders = EditorGUILayout.Toggle("Colliders", addColliders);

        if (GUILayout.Button("Générer"))
            Generate();
    }

    void LoadFile(string path)
    {
        filePath = path;
        EditorPrefs.SetString(LastFileKey, path);
        try
        {
            EditorUtility.DisplayProgressBar("Import OSM", "Lecture du fichier...", 0f);
            data = OsmData.Load(path);
        }
        catch (System.Exception e)
        {
            data = null;
            EditorUtility.DisplayDialog("Import OSM", "Lecture impossible : " + e.Message, "OK");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
        if (data != null)
            CenterOrigin();
    }

    void CenterOrigin()
    {
        originLat = (data.MinLat + data.MaxLat) / 2;
        originLon = (data.MinLon + data.MaxLon) / 2;
    }

    void Generate()
    {
        var builder = new SceneBuilder(data, new Projection(originLat, originLon))
        {
            LevelHeight = levelHeight,
            DefaultLevels = defaultLevels,
            SidewalkWidth = sidewalkWidth,
            AddColliders = addColliders,
        };
        var root = builder.Build(Path.GetFileNameWithoutExtension(filePath));
        Selection.activeGameObject = root;
        SceneView.FrameLastActiveSceneView();
    }

    // --- Lecture du fichier ---

    class Way
    {
        public long Id;
        public List<long> Nodes = new List<long>();
        public Dictionary<string, string> Tags = new Dictionary<string, string>();
    }

    class Relation
    {
        public long Id;
        public List<(long way, string role)> Members = new List<(long, string)>();
        public Dictionary<string, string> Tags = new Dictionary<string, string>();
    }

    class OsmData
    {
        public Dictionary<long, (double lat, double lon)> Nodes = new Dictionary<long, (double, double)>();
        public List<Way> Ways = new List<Way>();
        public Dictionary<long, Way> WaysById = new Dictionary<long, Way>();
        public List<Relation> Relations = new List<Relation>();
        public double MinLat, MinLon, MaxLat, MaxLon;
        public int BuildingCount;

        public static OsmData Load(string path)
        {
            var data = new OsmData();
            bool hasBounds = false;
            var settings = new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true, DtdProcessing = DtdProcessing.Ignore };
            using (var reader = XmlReader.Create(path, settings))
            {
                Dictionary<string, string> tags = null;
                Way way = null;
                Relation relation = null;
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element)
                        continue;
                    switch (reader.Name)
                    {
                        case "bounds":
                            hasBounds = true;
                            data.MinLat = Double(reader, "minlat");
                            data.MinLon = Double(reader, "minlon");
                            data.MaxLat = Double(reader, "maxlat");
                            data.MaxLon = Double(reader, "maxlon");
                            break;
                        case "node":
                            way = null;
                            relation = null;
                            tags = null;
                            if (!Deleted(reader))
                                data.Nodes[Long(reader, "id")] = (Double(reader, "lat"), Double(reader, "lon"));
                            break;
                        case "way":
                            relation = null;
                            way = Deleted(reader) ? null : new Way { Id = Long(reader, "id") };
                            tags = way?.Tags;
                            if (way != null)
                                data.Ways.Add(way);
                            break;
                        case "relation":
                            way = null;
                            relation = Deleted(reader) ? null : new Relation { Id = Long(reader, "id") };
                            tags = relation?.Tags;
                            if (relation != null)
                                data.Relations.Add(relation);
                            break;
                        case "nd":
                            way?.Nodes.Add(Long(reader, "ref"));
                            break;
                        case "member":
                            if (relation != null && reader.GetAttribute("type") == "way")
                                relation.Members.Add((Long(reader, "ref"), reader.GetAttribute("role") ?? ""));
                            break;
                        case "tag":
                            if (tags != null)
                                tags[reader.GetAttribute("k")] = reader.GetAttribute("v");
                            break;
                    }
                }
            }

            // Sans <bounds> (export Overpass), l'emprise est celle des points
            if (!hasBounds && data.Nodes.Count > 0)
            {
                data.MinLat = data.Nodes.Values.Min(n => n.lat);
                data.MaxLat = data.Nodes.Values.Max(n => n.lat);
                data.MinLon = data.Nodes.Values.Min(n => n.lon);
                data.MaxLon = data.Nodes.Values.Max(n => n.lon);
            }
            foreach (var w in data.Ways)
                data.WaysById[w.Id] = w;
            data.BuildingCount = data.Ways.Count(w => IsBuilding(w.Tags)) + data.Relations.Count(r => IsBuilding(r.Tags));
            return data;
        }

        // Fichiers édités dans JOSM : objets supprimés gardés avec action="delete"
        static bool Deleted(XmlReader r) => r.GetAttribute("action") == "delete" || r.GetAttribute("visible") == "false";
        static long Long(XmlReader r, string name) => long.Parse(r.GetAttribute(name), CultureInfo.InvariantCulture);
        static double Double(XmlReader r, string name) => double.Parse(r.GetAttribute(name), CultureInfo.InvariantCulture);
    }

    // Projection locale autour de l'origine avec les rayons de courbure WGS84 :
    // distances justes à quelques cm près sur un quartier, contrairement au Web Mercator (x1,5 à Paris)
    struct Projection
    {
        readonly double lat0, lon0, metersPerDegLat, metersPerDegLon;

        public Projection(double lat, double lon)
        {
            const double a = 6378137.0, e2 = 0.00669437999014;
            double phi = lat * System.Math.PI / 180.0;
            double s = 1 - e2 * System.Math.Sin(phi) * System.Math.Sin(phi);
            double meridian = a * (1 - e2) / System.Math.Pow(s, 1.5);
            double normal = a / System.Math.Sqrt(s);
            lat0 = lat;
            lon0 = lon;
            metersPerDegLat = meridian * System.Math.PI / 180.0;
            metersPerDegLon = normal * System.Math.Cos(phi) * System.Math.PI / 180.0;
        }

        public Vector2 Project(double lat, double lon) => new Vector2((float)((lon - lon0) * metersPerDegLon), (float)((lat - lat0) * metersPerDegLat));
    }

    // --- Classement des tags ---

    static string Tag(Dictionary<string, string> tags, string key) => tags.TryGetValue(key, out var v) ? v : null;

    static bool IsBuilding(Dictionary<string, string> tags)
    {
        var building = Tag(tags, "building");
        return building != null && building != "no" && building != "roof" && !IsUnderground(tags);
    }

    static bool IsUnderground(Dictionary<string, string> tags) =>
        Tag(tags, "tunnel") is "yes" or "culvert" || Tag(tags, "indoor") == "yes" || Tag(tags, "location") is "underground" or "indoor";

    static Ground? AreaCategory(Dictionary<string, string> tags)
    {
        if (IsUnderground(tags))
            return null;
        var areaHighway = Tag(tags, "area:highway");
        if (areaHighway != null)
            return areaHighway is "footway" or "pedestrian" or "path" ? Ground.Pieton : areaHighway == "cycleway" ? Ground.Cyclable : Ground.Route;
        var highway = Tag(tags, "highway");
        if (highway != null && Tag(tags, "area") == "yes")
            return highway is "pedestrian" or "footway" or "path" or "steps" ? Ground.Pieton : Ground.Route;
        if (Tag(tags, "place") == "square")
            return Ground.Pieton;
        if (Tag(tags, "amenity") == "parking" && Tag(tags, "parking") is null or "surface")
            return Ground.Parking;
        if (Tag(tags, "natural") == "water" || Tag(tags, "waterway") is "riverbank" or "dock" || Tag(tags, "landuse") is "basin" or "reservoir")
            return Ground.Eau;
        if (Tag(tags, "leisure") is "park" or "garden" or "playground" or "dog_park" or "pitch"
            || Tag(tags, "landuse") is "grass" or "forest" or "meadow" or "village_green" or "recreation_ground" or "cemetery" or "allotments" or "flowerbed"
            || Tag(tags, "natural") is "wood" or "scrub" or "grassland" or "heath")
            return Ground.Vegetation;
        return null;
    }

    // Type de sol et largeur par défaut (m) des voies linéaires
    static (Ground ground, float width)? LineCategory(Dictionary<string, string> tags)
    {
        if (Tag(tags, "area") == "yes" || IsUnderground(tags))
            return null;
        switch (Tag(tags, "highway"))
        {
            case "motorway": return (Ground.Route, 12f);
            case "trunk": case "primary": return (Ground.Route, 10f);
            case "secondary": return (Ground.Route, 8f);
            case "tertiary": return (Ground.Route, 7f);
            case "unclassified": case "residential": case "road": return (Ground.Route, 6f);
            case "motorway_link": case "trunk_link": case "primary_link": case "secondary_link": case "tertiary_link": return (Ground.Route, 5f);
            case "living_street": return (Ground.Route, 5f);
            case "busway": return (Ground.Route, 4f);
            case "service": return (Ground.Route, Tag(tags, "service") == "parking_aisle" ? 3f : 4f);
            case "track": return (Ground.Route, 3f);
            case "pedestrian": return (Ground.Pieton, 5f);
            case "footway": return Tag(tags, "footway") == "sidewalk" ? (Ground.Trottoir, -1f) : (Ground.Pieton, 2f);
            case "path": case "bridleway": return (Ground.Pieton, 1.5f);
            case "steps": return (Ground.Pieton, 2f);
            case "cycleway": return (Ground.Cyclable, 2f);
        }
        switch (Tag(tags, "railway"))
        {
            case "rail": case "tram": case "light_rail": return (Ground.Rail, 2f);
            case "narrow_gauge": return (Ground.Rail, 1.5f);
        }
        return null;
    }

    // "12", "12.5 m", "12,5", "40 ft" -> mètres. "5;6" -> 5
    static float? ParseMeters(string s)
    {
        if (string.IsNullOrEmpty(s))
            return null;
        var match = Regex.Match(s.Replace(',', '.'), @"\d+(\.\d+)?");
        if (!match.Success)
            return null;
        float value = float.Parse(match.Value, CultureInfo.InvariantCulture);
        return s.Contains("ft") || s.Contains("'") ? value * 0.3048f : value;
    }

    // --- Génération ---

    class Area
    {
        public string Id;
        public Dictionary<string, string> Tags;
        public List<Vector2> Outer;
        public List<List<Vector2>> Holes = new List<List<Vector2>>();
    }

    class SceneBuilder
    {
        public float LevelHeight, SidewalkWidth;
        public int DefaultLevels;
        public bool AddColliders;

        readonly OsmData data;
        readonly Projection projection;
        readonly Dictionary<Ground, MeshBuilder> ground = new Dictionary<Ground, MeshBuilder>();
        readonly List<Mesh> meshes = new List<Mesh>();

        public SceneBuilder(OsmData data, Projection projection)
        {
            this.data = data;
            this.projection = projection;
        }

        MeshBuilder GroundMesh(Ground g) => ground.TryGetValue(g, out var mb) ? mb : ground[g] = new MeshBuilder();
        static float Height(Ground g) => (int)g * GroundStep;

        public GameObject Build(string name)
        {
            var root = new GameObject($"OSM {name}");
            try
            {
                var buildingsRoot = Child(root.transform, "Batiments").transform;
                var groundRoot = Child(root.transform, "Sol").transform;
                var buildingMaterial = GetMaterial("Batiment", BuildingColor);

                // Sol de base sur toute l'emprise du fichier : ce qui reste visible est ni route, ni trottoir, ni bâtiment
                Vector2 min = Project(data.MinLat, data.MinLon), max = Project(data.MaxLat, data.MaxLon);
                GroundMesh(Ground.Sol).Quad(V(min, 0f), V(new Vector2(max.x, min.y), 0f), V(max, 0f), V(new Vector2(min.x, max.y), 0f), Vector3.up);

                var areas = CollectAreas();
                for (int i = 0; i < areas.Count; i++)
                {
                    if (i % 100 == 0)
                        EditorUtility.DisplayProgressBar("Import OSM", $"Bâtiments et zones ({i}/{areas.Count})", 0.8f * i / areas.Count);
                    var area = areas[i];
                    if (IsBuilding(area.Tags))
                        CreateBuilding(area, buildingsRoot, buildingMaterial);
                    else if (AreaCategory(area.Tags) is Ground category)
                        AddArea(GroundMesh(category), area, Height(category));
                }

                EditorUtility.DisplayProgressBar("Import OSM", "Routes et trottoirs", 0.85f);
                AddLines();

                EditorUtility.DisplayProgressBar("Import OSM", "Enregistrement des meshes", 0.9f);
                foreach (var kv in ground.OrderBy(kv => kv.Key))
                {
                    var go = Child(groundRoot, kv.Key.ToString());
                    AddMesh(go, kv.Value.ToMesh($"{name} {kv.Key}"), GetMaterial(kv.Key.ToString(), GroundColors[kv.Key]));
                }
                SaveMeshes(name);
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Undo.RegisterCreatedObjectUndo(root, "Importer OSM");
            return root;
        }

        Vector2 Project(double lat, double lon) => projection.Project(lat, lon);

        // Points d'un way, sans doublons consécutifs. null si des points manquent dans le fichier
        List<Vector2> Points(List<long> ids)
        {
            var points = new List<Vector2>(ids.Count);
            foreach (var id in ids)
            {
                if (!data.Nodes.TryGetValue(id, out var n))
                    return null;
                var p = Project(n.lat, n.lon);
                if (points.Count == 0 || (points[points.Count - 1] - p).sqrMagnitude > 1e-4f)
                    points.Add(p);
            }
            return points;
        }

        // Anneau fermé sans le point de fermeture répété
        List<Vector2> Ring(List<long> ids)
        {
            var points = Points(ids);
            if (points == null)
                return null;
            if (points.Count > 1 && (points[0] - points[points.Count - 1]).sqrMagnitude < 1e-4f)
                points.RemoveAt(points.Count - 1);
            return points.Count >= 3 ? points : null;
        }

        List<Area> CollectAreas()
        {
            var areas = new List<Area>();
            foreach (var way in data.Ways)
            {
                if (way.Nodes.Count < 4 || way.Nodes[0] != way.Nodes[way.Nodes.Count - 1])
                    continue;
                if (!IsBuilding(way.Tags) && AreaCategory(way.Tags) == null)
                    continue;
                var ring = Ring(way.Nodes);
                if (ring != null)
                    areas.Add(Oriented(new Area { Id = "w" + way.Id, Tags = way.Tags, Outer = ring }));
            }

            // Multipolygones : contours découpés en plusieurs ways (à recoller), cours intérieures en trous
            foreach (var relation in data.Relations)
            {
                if (Tag(relation.Tags, "type") != "multipolygon" || (!IsBuilding(relation.Tags) && AreaCategory(relation.Tags) == null))
                    continue;
                List<List<Vector2>> Rings(bool inner) => Stitch(relation.Members
                        .Where(m => (m.role == "inner") == inner && data.WaysById.ContainsKey(m.way))
                        .Select(m => data.WaysById[m.way].Nodes))
                    .Select(Ring).Where(r => r != null).ToList();

                var inners = Rings(true);
                foreach (var outer in Rings(false))
                {
                    var area = new Area { Id = "r" + relation.Id, Tags = relation.Tags, Outer = outer };
                    area.Holes.AddRange(inners.Where(h => PointInPolygon(h[0], outer)));
                    areas.Add(Oriented(area));
                }
            }
            return areas;
        }

        void CreateBuilding(Area area, Transform parent, Material material)
        {
            var tags = area.Tags;
            float bottom = ParseMeters(Tag(tags, "min_height")) ?? (ParseMeters(Tag(tags, "building:min_level")) ?? 0f) * LevelHeight;
            float top = ParseMeters(Tag(tags, "height"))
                ?? ((ParseMeters(Tag(tags, "building:levels")) ?? DefaultLevels) + (ParseMeters(Tag(tags, "roof:levels")) ?? 0f)) * LevelHeight;
            if (top < bottom + 0.5f)
                top = bottom + LevelHeight;

            // Pivot au centre de l'emprise, au niveau du sol : pratique pour remplacer le bloc par un mesh plus tard
            var center = area.Outer.Aggregate(Vector2.zero, (sum, p) => sum + p) / area.Outer.Count;
            var outer = area.Outer.Select(p => p - center).ToList();
            var holes = area.Holes.Select(h => h.Select(p => p - center).ToList()).ToList();

            var mb = new MeshBuilder();
            var (polygon, triangles) = Triangulate(outer, holes);
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector2 a = polygon[triangles[i]], b = polygon[triangles[i + 1]], c = polygon[triangles[i + 2]];
                mb.Tri(V(a, top), V(b, top), V(c, top), Vector3.up);
                if (bottom > 0.01f)
                    mb.Tri(V(a, bottom), V(b, bottom), V(c, bottom), Vector3.down);
            }
            Walls(mb, outer, bottom, top);
            foreach (var hole in holes)
                Walls(mb, hole, bottom, top);

            var label = Tag(tags, "name") ?? (Tag(tags, "addr:housenumber") != null ? $"{Tag(tags, "addr:housenumber")} {Tag(tags, "addr:street")}".Trim() : null);
            var go = Child(parent, label != null ? $"Batiment {area.Id} - {label}" : $"Batiment {area.Id}");
            go.transform.localPosition = V(center, 0f);
            AddMesh(go, mb.ToMesh(go.name), material);
        }

        // Anneaux orientés : contour anti-horaire et trous horaires (vus du dessus), la normale sortante
        // de l'arête p -> q est alors toujours à droite
        static void Walls(MeshBuilder mb, List<Vector2> ring, float bottom, float top)
        {
            for (int i = 0; i < ring.Count; i++)
            {
                Vector2 p = ring[i], q = ring[(i + 1) % ring.Count], d = q - p;
                var outward = new Vector3(d.y, 0f, -d.x);
                mb.Quad(V(p, bottom), V(q, bottom), V(q, top), V(p, top), outward);
            }
        }

        static void AddArea(MeshBuilder mb, Area area, float y)
        {
            var (polygon, triangles) = Triangulate(area.Outer, area.Holes);
            for (int i = 0; i < triangles.Count; i += 3)
                mb.Tri(V(polygon[triangles[i]], y), V(polygon[triangles[i + 1]], y), V(polygon[triangles[i + 2]], y), Vector3.up);
        }

        void AddLines()
        {
            // Carrefours : un disque par point partagé entre plusieurs routes bouche les trous entre les bandes
            var junctions = new Dictionary<long, (int count, float radius)>();

            foreach (var way in data.Ways)
            {
                if (!(LineCategory(way.Tags) is { } line))
                    continue;
                var (category, defaultWidth) = line;
                var points = Points(way.Nodes);
                if (points == null || points.Count < 2)
                    continue;

                float width = ParseMeters(Tag(way.Tags, "width")) ?? (category == Ground.Trottoir ? SidewalkWidth : defaultWidth);
                if (category == Ground.Route && Tag(way.Tags, "width") == null && ParseMeters(Tag(way.Tags, "lanes")) is float lanes)
                    width = lanes * 3.25f;
                AddStrip(GroundMesh(category), points, -width / 2, width / 2, Height(category));

                if (category != Ground.Route)
                    continue;
                AddSidewalks(way.Tags, points, width);
                foreach (var id in way.Nodes.Distinct())
                {
                    junctions.TryGetValue(id, out var j);
                    junctions[id] = (j.count + 1, Mathf.Max(j.radius, width / 2));
                }
            }

            var roads = GroundMesh(Ground.Route);
            foreach (var kv in junctions.Where(kv => kv.Value.count > 1))
            {
                var n = data.Nodes[kv.Key];
                AddDisc(roads, Project(n.lat, n.lon), kv.Value.radius, Height(Ground.Route));
            }
        }

        // Trottoirs renseignés sur la route (sidewalk=both/left/right). "separate" = way footway=sidewalk à part, déjà dessiné
        void AddSidewalks(Dictionary<string, string> tags, List<Vector2> points, float roadWidth)
        {
            var both = Tag(tags, "sidewalk:both") ?? Tag(tags, "sidewalk");
            bool left = both is "both" or "yes" or "left", right = both is "both" or "yes" or "right";
            if (Tag(tags, "sidewalk:left") is string l)
                left = l == "yes";
            if (Tag(tags, "sidewalk:right") is string r)
                right = r == "yes";

            float width = ParseMeters(Tag(tags, "sidewalk:both:width") ?? Tag(tags, "sidewalk:width")) ?? SidewalkWidth;
            float half = roadWidth / 2, y = Height(Ground.Trottoir);
            if (left)
                AddStrip(GroundMesh(Ground.Trottoir), points, half, half + (ParseMeters(Tag(tags, "sidewalk:left:width")) ?? width), y);
            if (right)
                AddStrip(GroundMesh(Ground.Trottoir), points, -half - (ParseMeters(Tag(tags, "sidewalk:right:width")) ?? width), -half, y);
        }

        void AddMesh(GameObject go, Mesh mesh, Material material)
        {
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (AddColliders)
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // Meshes rangés dans un seul asset plutôt que dans la scène : sinon le .unity pèse des dizaines de Mo
        void SaveMeshes(string name)
        {
            EnsureFolder(OutputDir);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{OutputDir}/{name}.asset");
            AssetDatabase.CreateAsset(meshes[0], path);
            for (int i = 1; i < meshes.Count; i++)
                AssetDatabase.AddObjectToAsset(meshes[i], path);
            AssetDatabase.SaveAssets();
        }
    }

    // --- Géométrie ---

    static Vector3 V(Vector2 p, float y) => new Vector3(p.x, y, p.y);
    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    static float SignedArea(List<Vector2> ring)
    {
        float area = 0f;
        for (int i = 0; i < ring.Count; i++)
            area += Cross(ring[i], ring[(i + 1) % ring.Count]);
        return area / 2;
    }

    static Area Oriented(Area area)
    {
        if (SignedArea(area.Outer) < 0)
            area.Outer.Reverse();
        foreach (var hole in area.Holes.Where(h => SignedArea(h) > 0))
            hole.Reverse();
        return area;
    }

    static bool PointInPolygon(Vector2 p, List<Vector2> ring)
    {
        bool inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            if ((ring[i].y > p.y) != (ring[j].y > p.y)
                && p.x < (ring[j].x - ring[i].x) * (p.y - ring[i].y) / (ring[j].y - ring[i].y) + ring[i].x)
                inside = !inside;
        }
        return inside;
    }

    // Recolle les ways d'un multipolygone bout à bout en anneaux fermés (ceux qui ne se ferment pas sont ignorés)
    static List<List<long>> Stitch(IEnumerable<List<long>> segments)
    {
        var open = segments.Where(s => s.Count >= 2).Select(s => new List<long>(s)).ToList();
        var rings = new List<List<long>>();
        while (open.Count > 0)
        {
            var ring = open[0];
            open.RemoveAt(0);
            while (ring[0] != ring[ring.Count - 1])
            {
                long end = ring[ring.Count - 1];
                int k = open.FindIndex(s => s[0] == end || s[s.Count - 1] == end);
                if (k < 0)
                    break;
                var next = open[k];
                open.RemoveAt(k);
                if (next[0] != end)
                    next.Reverse();
                ring.AddRange(next.Skip(1));
            }
            if (ring.Count >= 4 && ring[0] == ring[ring.Count - 1])
                rings.Add(ring);
        }
        return rings;
    }

    // Bande entre deux décalages latéraux (gauche > 0) le long d'une polyligne, avec jointures en onglet
    static void AddStrip(MeshBuilder mb, List<Vector2> points, float from, float to, float y)
    {
        int n = points.Count;
        bool closed = n > 2 && (points[0] - points[n - 1]).sqrMagnitude < 1e-4f;
        var offsets = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            Vector2? previous = i > 0 ? points[i] - points[i - 1] : closed ? points[n - 1] - points[n - 2] : (Vector2?)null;
            Vector2? next = i < n - 1 ? points[i + 1] - points[i] : closed ? points[1] - points[0] : (Vector2?)null;
            var n1 = Left(previous ?? next.Value);
            var n2 = Left(next ?? previous.Value);
            var miter = n1 + n2;
            miter = miter.sqrMagnitude < 1e-6f ? n1 : miter.normalized;
            // Onglet limité à 2x la largeur dans les virages serrés, sinon il part en pointe
            offsets[i] = miter / Mathf.Max(Vector2.Dot(miter, n1), 0.5f);
        }
        for (int i = 0; i < n - 1; i++)
        {
            Vector2 p = points[i], q = points[i + 1];
            mb.Quad(V(p + offsets[i] * from, y), V(p + offsets[i] * to, y), V(q + offsets[i + 1] * to, y), V(q + offsets[i + 1] * from, y), Vector3.up);
        }
    }

    static Vector2 Left(Vector2 direction) => new Vector2(-direction.y, direction.x).normalized;

    static void AddDisc(MeshBuilder mb, Vector2 center, float radius, float y)
    {
        const int segments = 16;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * 2 * Mathf.PI / segments, a1 = (i + 1) * 2 * Mathf.PI / segments;
            mb.Tri(V(center, y), V(center + radius * new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)), y), V(center + radius * new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)), y), Vector3.up);
        }
    }

    // Triangulation par ear clipping. Les trous sont d'abord raccordés au contour par un pont
    // (aller-retour vers le sommet visible le plus proche) pour n'avoir qu'un seul polygone
    static (List<Vector2> polygon, List<int> triangles) Triangulate(List<Vector2> outer, List<List<Vector2>> holes)
    {
        var polygon = new List<Vector2>(outer);
        var remaining = holes.Where(h => h.Count >= 3).OrderByDescending(h => h.Max(p => p.x)).ToList();
        while (remaining.Count > 0)
        {
            var hole = remaining[0];
            remaining.RemoveAt(0);
            BridgeHole(polygon, hole, remaining);
        }
        return (polygon, EarClip(polygon));
    }

    static void BridgeHole(List<Vector2> polygon, List<Vector2> hole, List<List<Vector2>> otherHoles)
    {
        int mi = 0;
        for (int i = 1; i < hole.Count; i++)
            if (hole[i].x > hole[mi].x)
                mi = i;
        var m = hole[mi];

        int best = -1, nearest = 0;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < polygon.Count; i++)
        {
            float d = (polygon[i] - m).sqrMagnitude;
            if (d < (polygon[nearest] - m).sqrMagnitude)
                nearest = i;
            if (d >= bestDistance)
                continue;
            if (!SegmentClear(m, polygon[i], polygon) || !SegmentClear(m, polygon[i], hole) || otherHoles.Any(h => !SegmentClear(m, polygon[i], h)))
                continue;
            best = i;
            bestDistance = d;
        }
        if (best < 0)
            best = nearest;

        var bridge = new List<Vector2>(hole.Count + 2);
        for (int j = 0; j <= hole.Count; j++)
            bridge.Add(hole[(mi + j) % hole.Count]);
        bridge.Add(polygon[best]);
        polygon.InsertRange(best + 1, bridge);
    }

    // Le segment a-b ne coupe aucune arête de l'anneau (les arêtes qui touchent a ou b ne comptent pas)
    static bool SegmentClear(Vector2 a, Vector2 b, List<Vector2> ring)
    {
        for (int i = 0; i < ring.Count; i++)
        {
            Vector2 p = ring[i], q = ring[(i + 1) % ring.Count];
            if (p == a || p == b || q == a || q == b)
                continue;
            float d1 = Cross(b - a, p - a), d2 = Cross(b - a, q - a), d3 = Cross(q - p, a - p), d4 = Cross(q - p, b - p);
            if (d1 * d2 < 0 && d3 * d4 < 0)
                return false;
        }
        return true;
    }

    static List<int> EarClip(List<Vector2> polygon)
    {
        var triangles = new List<int>();
        var remaining = Enumerable.Range(0, polygon.Count).ToList();
        int i = 0, failures = 0;
        while (remaining.Count > 3)
        {
            if (failures > remaining.Count)
            {
                // Plus d'oreille (polygone OSM qui se recoupe) : on retire le sommet le plus plat pour avancer
                int flattest = 0;
                float smallest = float.MaxValue;
                for (int k = 0; k < remaining.Count; k++)
                {
                    float c = Mathf.Abs(Corner(polygon, remaining, k));
                    if (c < smallest)
                    {
                        smallest = c;
                        flattest = k;
                    }
                }
                remaining.RemoveAt(flattest);
                failures = 0;
                continue;
            }

            i %= remaining.Count;
            int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c2 = remaining[(i + 1) % remaining.Count];
            if (Corner(polygon, remaining, i) > 1e-6f && !ContainsOther(polygon, remaining, a, b, c2))
            {
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c2);
                remaining.RemoveAt(i);
                failures = 0;
            }
            else
            {
                i++;
                failures++;
            }
        }
        if (remaining.Count == 3)
            triangles.AddRange(remaining);
        return triangles;
    }

    // > 0 si le sommet k est convexe (polygone anti-horaire)
    static float Corner(List<Vector2> polygon, List<int> remaining, int k)
    {
        int n = remaining.Count;
        Vector2 a = polygon[remaining[(k + n - 1) % n]], b = polygon[remaining[k]], c = polygon[remaining[(k + 1) % n]];
        return Cross(b - a, c - b);
    }

    static bool ContainsOther(List<Vector2> polygon, List<int> remaining, int ia, int ib, int ic)
    {
        Vector2 a = polygon[ia], b = polygon[ib], c = polygon[ic];
        foreach (int j in remaining)
        {
            var p = polygon[j];
            // Les sommets dupliqués par les ponts des trous sont au même endroit qu'un coin : pas un obstacle
            if (j == ia || j == ib || j == ic || p == a || p == b || p == c)
                continue;
            if (Cross(b - a, p - a) >= 0 && Cross(c - b, p - b) >= 0 && Cross(a - c, p - c) >= 0)
                return true;
        }
        return false;
    }

    class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int> triangles = new List<int>();

        // Sommets non partagés : normales plates, et l'ordre est retourné si besoin pour que la face regarde vers normal
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0)
                (b, c) = (c, b);
            int i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(i);
            triangles.Add(i + 1);
            triangles.Add(i + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            Tri(a, b, c, normal);
            Tri(a, c, d, normal);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // --- Assets ---

    static GameObject Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    // Matériaux créés une seule fois dans Assets/OSM/Materials : les couleurs peuvent y être retouchées
    static Material GetMaterial(string name, Color color)
    {
        var path = $"{MaterialDir}/OSM_{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        EnsureFolder(MaterialDir);
        material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
