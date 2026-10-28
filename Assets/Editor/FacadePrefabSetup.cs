using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Met les prefabs Generic* et Shop* au format attendu par l'import OSM et l'habillage des rideaux :
// root sans rotation (pivot en bas au centre du mur, +Z vers la rue), mesh Blender en enfant "Mesh".
// Les Generic reçoivent FacadeGeneric, un BoxCollider et leurs emplacements RideauDeFer (calculés depuis la texture).
// Les instances déjà posées dans les scènes sont recalées pour ne pas bouger.
static class FacadePrefabSetup
{
    const string BatimentsDir = "Assets/Prefabs/Batiments/";
    const string ObstacleTag = "Obstacle";

    // Rideaux de fer dans la texture de façade : u gauche, u droite, v haut (v = 0 en bas de la texture)
    static readonly Dictionary<string, (float u0, float u1, float vTop)[]> Rideaux = new Dictionary<string, (float, float, float)[]>
    {
        { "Generic1", new[] { (0.303f, 0.976f, 0.1875f) } },
        { "Generic2", new[] { (0.053f, 0.427f, 0.164f), (0.608f, 0.998f, 0.138f) } },
        { "Generic3", new[] { (0.001f, 0.328f, 0.146f), (0.650f, 0.999f, 0.178f) } },
        { "Generic4", new[] { (0.001f, 0.328f, 0.169f), (0.650f, 0.999f, 0.196f) } },
        { "Generic5", new[] { (0.053f, 0.427f, 0.162f), (0.608f, 0.998f, 0.130f) } },
    };

    // Rotation du mesh Blender (face avant = -Y du mesh) pour que la façade regarde +Z
    static readonly Quaternion MeshRotation = Quaternion.Euler(270f, 0f, 0f);

    // Scène où les shops ont été posés à la main sur Generic4 : sert de référence pour leur orientation
    const string ReferenceScene = "Assets/Scenes/TestLights.unity";

    [MenuItem("FOUVELIB/Bâtiments/Préparer les prefabs Generic et Shop")]
    static void Run()
    {
        var generics = Enumerable.Range(1, 5).Select(i => $"Generic{i}").ToArray();
        var shops = new[] { "Shop1", "Shop2" };
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        var setup = EditorSceneManager.GetSceneManagerSetup();

        // Orientation des shops relative à la façade, lue dans la scène de référence avant toute modification
        var shopRelative = ReadShopOrientations(shops);

        // Poses des instances relevées avant modification : ensuite le scale non overridé prendrait celui du nouveau root
        var names = generics.Concat(shops).ToList();
        var guids = names.Select(n => AssetDatabase.AssetPathToGUID(PrefabPath(n))).ToList();
        var scenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => { var text = System.IO.File.ReadAllText(p); return guids.Any(text.Contains); }).ToList();
        var poses = scenes.ToDictionary(p => p, ReadInstancePoses);

        var meshLocal = new Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)>();
        foreach (var name in generics)
            meshLocal[name] = SetupGeneric(name);
        foreach (var name in shops)
            meshLocal[name] = SetupShop(name, shopRelative[name]);

        foreach (var scenePath in scenes)
            FixInstances(scenePath, poses[scenePath], meshLocal);

        EditorSceneManager.RestoreSceneManagerSetup(setup);
        AssetDatabase.SaveAssets();
        Debug.Log("Prefabs Generic/Shop préparés.");
    }

    // --- Generic ---

    static (Vector3, Quaternion, Vector3) SetupGeneric(string name)
    {
        var path = BatimentsDir + name + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.transform.Find("Mesh") != null)
                throw new System.InvalidOperationException(name + " est déjà préparé");
            var mesh = root.GetComponent<MeshFilter>().sharedMesh;
            var front = FrontFaces(mesh);

            // Mur de façade : profondeur de la face avant au centre du rez-de-chaussée
            float wall = DepthAt(front, 0f, 1.5f) ?? 0f;
            var meshPos = MeshRotation * new Vector3(0f, -wall, 0f);
            var child = MoveMeshToChild(root, meshPos, MeshRotation, Vector3.one);

            var b = mesh.bounds;
            var facade = root.AddComponent<FacadeGeneric>();
            facade.width = b.size.x;
            facade.height = b.size.z;
            facade.depth = b.max.y - wall;

            // Collider du mur au fond (les balcons qui dépassent ne bloquent pas le trottoir)
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(b.center.x, b.size.z / 2, -facade.depth / 2);
            box.size = new Vector3(b.size.x, b.size.z, facade.depth);
            root.tag = ObstacleTag;

            // Emplacements des rideaux : rectangle de texture -> position sur la face avant par le mapping UV
            var (ax, bx, az, bz) = FitUv(mesh, front);
            var rideauxRoot = new GameObject("Rideaux").transform;
            rideauxRoot.SetParent(root.transform, false);
            int i = 0;
            foreach (var (u0, u1, vTop) in Rideaux[name])
            {
                float x0 = ax * u0 + bx, x1 = ax * u1 + bx, zBottom = Mathf.Max(0f, az * 0f + bz), zTop = az * vTop + bz;
                float cx = (x0 + x1) / 2;
                float depth = DepthAt(front, cx, (zBottom + zTop) / 2) ?? wall;
                var go = new GameObject($"Rideau{++i}");
                go.transform.SetParent(rideauxRoot, false);
                go.transform.localPosition = new Vector3(cx, zBottom, wall - depth);
                go.AddComponent<RideauDeFer>().size = new Vector2(Mathf.Abs(x1 - x0), zTop - zBottom);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return (child.localPosition, child.localRotation, child.localScale);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Triangles de la face avant (normale -Y du mesh Blender)
    static List<(Vector3 a, Vector3 b, Vector3 c, int ia, int ib, int ic)> FrontFaces(Mesh mesh)
    {
        var v = mesh.vertices;
        var t = mesh.triangles;
        var result = new List<(Vector3, Vector3, Vector3, int, int, int)>();
        for (int i = 0; i < t.Length; i += 3)
        {
            Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
            if (Vector3.Cross(b - a, c - a).normalized.y < -0.9f)
                result.Add((a, b, c, t[i], t[i + 1], t[i + 2]));
        }
        return result;
    }

    // Profondeur (y du mesh) de la face avant la plus en avant au point (x, z)
    static float? DepthAt(List<(Vector3 a, Vector3 b, Vector3 c, int ia, int ib, int ic)> front, float x, float z)
    {
        float? best = null;
        var p = new Vector2(x, z);
        foreach (var (a, b, c, _, _, _) in front)
        {
            Vector2 a2 = new Vector2(a.x, a.z), b2 = new Vector2(b.x, b.z), c2 = new Vector2(c.x, c.z);
            float d = Cross(b2 - a2, c2 - a2);
            if (Mathf.Abs(d) < 1e-8f)
                continue;
            float l1 = Cross(b2 - p, c2 - p) / d, l2 = Cross(c2 - p, a2 - p) / d, l3 = 1 - l1 - l2;
            if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f)
                continue;
            float y = l1 * a.y + l2 * b.y + l3 * c.y;
            if (best == null || y < best)
                best = y;
        }
        return best;
    }

    static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // Mapping planaire de la face avant : x = ax * u + bx, z = az * v + bz (moindres carrés)
    static (float ax, float bx, float az, float bz) FitUv(Mesh mesh, List<(Vector3 a, Vector3 b, Vector3 c, int ia, int ib, int ic)> front)
    {
        var v = mesh.vertices;
        var uv = mesh.uv;
        var ids = front.SelectMany(f => new[] { f.ia, f.ib, f.ic }).Distinct().ToList();
        var (ax, bx) = Fit(ids.Select(i => (uv[i].x, v[i].x)));
        var (az, bz) = Fit(ids.Select(i => (uv[i].y, v[i].z)));
        return (ax, bx, az, bz);
    }

    static (float a, float b) Fit(IEnumerable<(float x, float y)> points)
    {
        var p = points.ToList();
        float mx = p.Average(q => q.x), my = p.Average(q => q.y);
        float sxy = p.Sum(q => (q.x - mx) * (q.y - my)), sxx = p.Sum(q => (q.x - mx) * (q.x - mx));
        float a = sxy / sxx;
        return (a, my - a * mx);
    }

    // --- Shop ---

    static Dictionary<string, Quaternion> ReadShopOrientations(string[] shops)
    {
        var result = shops.ToDictionary(s => s, s => Quaternion.identity);
        var scene = EditorSceneManager.OpenScene(ReferenceScene, OpenSceneMode.Single);
        var instances = scene.GetRootGameObjects();
        var facade = instances.FirstOrDefault(g => SourceName(g) == "Generic4");
        if (facade == null)
            return result;
        // Façade normalisée = mesh tourné de MeshRotation : sa rotation monde est celle du mesh sans MeshRotation
        var facadeRotation = facade.transform.rotation * Quaternion.Inverse(MeshRotation);
        foreach (var shop in shops)
        {
            var instance = instances.FirstOrDefault(g => SourceName(g) == shop);
            if (instance != null)
                result[shop] = Quaternion.Inverse(facadeRotation) * instance.transform.rotation;
        }
        return result;
    }

    static (Vector3, Quaternion, Vector3) SetupShop(string name, Quaternion relativeRotation)
    {
        var path = PrefabPath(name);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.transform.Find("Mesh") != null)
                throw new System.InvalidOperationException(name + " est déjà préparé");
            var scale = root.transform.localScale;
            var child = MoveMeshToChild(root, Vector3.zero, relativeRotation, scale);

            // Pivot en bas au centre, dos du shop contre le mur (z = 0)
            var renderer = child.GetComponent<MeshRenderer>();
            var bounds = LocalBounds(root.transform, renderer);
            child.localPosition = -new Vector3(bounds.center.x, bounds.min.y, bounds.min.z);
            bounds = LocalBounds(root.transform, renderer);

            var box = root.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
            root.tag = ObstacleTag;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return (child.localPosition, child.localRotation, child.localScale);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Boîte englobante d'un renderer dans l'espace local de root
    public static Bounds LocalBounds(Transform root, Renderer renderer)
    {
        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
        var toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
        var b = mesh.bounds;
        var result = new Bounds(toRoot.MultiplyPoint3x4(b.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            result.Encapsulate(toRoot.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))));
        return result;
    }

    // --- Commun ---

    // Déplace MeshFilter/MeshRenderer (et les enfants existants) du root vers un enfant "Mesh", root remis à zéro
    static Transform MoveMeshToChild(GameObject root, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var oldChildren = root.transform.Cast<Transform>().ToList();
        var child = new GameObject("Mesh").transform;
        child.SetParent(root.transform, false);
        child.gameObject.layer = root.layer;
        GameObjectUtility.SetStaticEditorFlags(child.gameObject, GameObjectUtility.GetStaticEditorFlags(root));

        var filter = root.GetComponent<MeshFilter>();
        var renderer = root.GetComponent<MeshRenderer>();
        child.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        var newRenderer = child.gameObject.AddComponent<MeshRenderer>();
        EditorUtility.CopySerialized(renderer, newRenderer);
        Object.DestroyImmediate(renderer);
        Object.DestroyImmediate(filter);

        // Les enfants (lumières des shops) gardent leur position relative au mesh
        foreach (var c in oldChildren)
            c.SetParent(child, false);

        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        child.localPosition = position;
        child.localRotation = rotation;
        child.localScale = scale;
        return child;
    }

    static string SourceName(GameObject go)
    {
        if (!PrefabUtility.IsAnyPrefabInstanceRoot(go))
            return null;
        var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
        return path.StartsWith(BatimentsDir) || path.StartsWith(Habillage.ShopsDir) ? System.IO.Path.GetFileNameWithoutExtension(path) : null;
    }

    static string PrefabPath(string name) => (name.StartsWith("Shop") ? Habillage.ShopsDir + "/" : BatimentsDir) + name + ".prefab";

    class InstancePose
    {
        public string Prefab;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
    }

    static IEnumerable<Transform> AllTransforms(UnityEngine.SceneManagement.Scene scene) =>
        scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true));

    // Clé d'une instance : chemin dans la hiérarchie avec index de frère (les noms peuvent se répéter)
    static string Key(Transform t) => t.parent == null ? $"{t.GetSiblingIndex()}:{t.name}" : $"{Key(t.parent)}/{t.GetSiblingIndex()}:{t.name}";

    static Dictionary<string, InstancePose> ReadInstancePoses(string scenePath)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var result = new Dictionary<string, InstancePose>();
        foreach (var t in AllTransforms(scene))
        {
            var name = SourceName(t.gameObject);
            if (name != null)
                result[Key(t)] = new InstancePose { Prefab = name, Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale };
        }
        return result;
    }

    // Le mesh de chaque instance garde sa pose : le root est déplacé, un scale d'instance différent passe sur l'enfant Mesh
    static void FixInstances(string scenePath, Dictionary<string, InstancePose> poses, Dictionary<string, (Vector3 pos, Quaternion rot, Vector3 scale)> meshLocal)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int count = 0;
        foreach (var t in AllTransforms(scene).ToList())
        {
            if (!poses.TryGetValue(Key(t), out var old) || !meshLocal.TryGetValue(old.Prefab, out var local))
                continue;
            var newRot = old.Rotation * Quaternion.Inverse(local.rot);
            t.localRotation = newRot;
            t.localScale = Vector3.one;
            t.localPosition = old.Position - newRot * local.pos;
            if ((old.Scale - local.scale).sqrMagnitude > 1e-8f)
                t.Find("Mesh").localScale = old.Scale;
            count++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"{scenePath} : {count} instances recalées");
    }
}
