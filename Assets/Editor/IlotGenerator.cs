using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

// Génère un îlot central type Pigalle : Extremity + suite de Middle (M) / MiddleDown (D) + Extremity retournée.
// Les pièces sont les prefabs de Assets/Prefabs/Ilot, l'îlot est créé à la position de la Scene view.
class IlotGenerator : EditorWindow
{
    const string PrefabDir = "Assets/Prefabs/Ilot/";

    // Orientation de chaque pièce dans l'îlot, comme le layout d'origine de ProBuilderTest
    const float ExtremityStartYaw = 0f, ExtremityEndYaw = 180f, MiddleYaw = 0f, MiddleDownYaw = 180f;

    int middleCount = 1;
    int middleDownCount = 2;
    string customSequence = "";

    [MenuItem("FOUVELIB/Générer un îlot...")]
    static void Open() => GetWindow<IlotGenerator>(true, "Générateur d'îlot");

    void OnGUI()
    {
        middleCount = Mathf.Max(0, EditorGUILayout.IntField("Middle", middleCount));
        middleDownCount = Mathf.Max(0, EditorGUILayout.IntField("MiddleDown", middleDownCount));
        customSequence = EditorGUILayout.TextField(new GUIContent("Séquence perso", "Ex : DMDMD (M = Middle, D = MiddleDown). Vide = MiddleDown répartis automatiquement"), customSequence);

        var sequence = BuildSequence();
        var pieces = LoadPieces(sequence);
        if (pieces == null)
        {
            EditorGUILayout.HelpBox("Prefabs manquants dans " + PrefabDir + " (Extremity, Middle, MiddleDown)", MessageType.Error);
            return;
        }

        float length = pieces.Sum(p => Footprint(p.prefab, p.yaw).size.y);
        EditorGUILayout.HelpBox($"E {sequence} E   —   longueur {length:0.#} m", MessageType.None);
        if (GUILayout.Button("Générer"))
            Generate(sequence, pieces);
    }

    string BuildSequence()
    {
        var custom = new string(customSequence.ToUpperInvariant().Where(c => c == 'M' || c == 'D').ToArray());
        if (customSequence.Trim().Length > 0)
            return custom;

        // Les MiddleDown sont répartis uniformément dans les trous autour des Middle (ex : 1 M + 2 D -> D M D)
        int gaps = middleCount + 1;
        var downsPerGap = new int[gaps];
        for (int i = 0; i < middleDownCount; i++)
            downsPerGap[Mathf.FloorToInt((i + 0.5f) * gaps / middleDownCount)]++;

        var sequence = "";
        for (int g = 0; g < gaps; g++)
        {
            sequence += new string('D', downsPerGap[g]);
            if (g < middleCount)
                sequence += "M";
        }
        return sequence;
    }

    static List<(GameObject prefab, float yaw)> LoadPieces(string sequence)
    {
        var extremity = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Extremity.prefab");
        var middle = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Middle.prefab");
        var middleDown = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "MiddleDown.prefab");
        if (extremity == null || middle == null || middleDown == null)
            return null;

        var pieces = new List<(GameObject, float)> { (extremity, ExtremityStartYaw) };
        pieces.AddRange(sequence.Select(c => c == 'M' ? (middle, MiddleYaw) : (middleDown, MiddleDownYaw)));
        pieces.Add((extremity, ExtremityEndYaw));
        return pieces;
    }

    // Emprise au sol (x, z) de la pièce une fois tournée de yaw autour de son pivot,
    // arrondie à 10 cm près pour absorber les vertices un peu décalés (13.51 au lieu de 13.5)
    static Rect Footprint(GameObject prefab, float yaw)
    {
        var rotation = Quaternion.Euler(0f, yaw, 0f);
        var points = prefab.GetComponent<ProBuilderMesh>().positions.Select(p => rotation * p).ToList();
        float minX = RoundDm(points.Min(p => p.x)), minZ = RoundDm(points.Min(p => p.z));
        return new Rect(minX, minZ, RoundDm(points.Max(p => p.x)) - minX, RoundDm(points.Max(p => p.z)) - minZ);
    }

    static float RoundDm(float v) => Mathf.Round(v * 10f) / 10f;

    void Generate(string sequence, List<(GameObject prefab, float yaw)> pieces)
    {
        var pivot = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
        var position = new Vector3(Mathf.Round(pivot.x), 0f, Mathf.Round(pivot.z));

        // Le dessous de l'îlot est posé sur ce qu'il y a sous la Scene view (la route),
        // sinon le bas des rampes flotte au-dessus et fait une marche
        float baseY = pieces.Min(p => p.prefab.GetComponent<ProBuilderMesh>().positions.Min(v => v.y));
        float groundY = Physics.Raycast(new Vector3(position.x, pivot.y + 5f, position.z), Vector3.down, out var hit, 100f) ? hit.point.y : 0f;
        position.y = groundY - baseY;

        var root = new GameObject($"Ilot (E{sequence}E)");
        root.transform.position = position;
        Undo.RegisterCreatedObjectUndo(root, "Générer un îlot");

        // Pièces empilées le long de +z à partir de 0, centrées en x sur le root
        float z = 0f;
        foreach (var (prefab, yaw) in pieces)
        {
            var footprint = Footprint(prefab, yaw);
            var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            piece.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            piece.transform.localPosition = new Vector3(-footprint.center.x, 0f, z - footprint.yMin);
            z += footprint.height;
        }
        BuildCollider(root);

        Selection.activeGameObject = root;
        SceneView.FrameLastActiveSceneView();
    }

    [MenuItem("FOUVELIB/Recalculer le collider de l'îlot")]
    static void RebuildSelectedColliders()
    {
        // Sélection = le root de l'îlot ou une de ses pièces
        var roots = Selection.gameObjects
            .Select(go => go.GetComponent<ProBuilderMesh>() != null && go.transform.parent != null ? go.transform.parent.gameObject : go)
            .Distinct();
        foreach (var root in roots)
            BuildCollider(root);
    }

    [MenuItem("FOUVELIB/Recalculer le collider de l'îlot", true)]
    static bool CanRebuildSelectedColliders() => Selection.gameObjects.Any(go => go.GetComponentInChildren<ProBuilderMesh>() != null);

    // Un seul MeshCollider soudé pour tout l'îlot au lieu d'un par pièce : PhysX ne lisse les arêtes
    // qu'entre triangles d'un même mesh qui partagent leurs vertices. Sinon le vélo accroche les
    // jonctions entre pièces et les arêtes internes, d'où les bumps sur les faces planes.
    static void BuildCollider(GameObject root)
    {
        var pieces = root.GetComponentsInChildren<ProBuilderMesh>();
        if (pieces.Length == 0)
            return;

        var toRoot = root.transform.worldToLocalMatrix;
        var piecePoints = pieces.Select(p =>
        {
            var matrix = toRoot * p.transform.localToWorldMatrix;
            return p.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
        }).ToList();

        // Plans z où une pièce touche sa voisine : les faces de bout qui s'y trouvent sont cachées dans l'îlot
        var ends = piecePoints.SelectMany(points => new[] { points.Min(v => v.z), points.Max(v => v.z) }).ToList();
        var seams = ends.Where(e => ends.Count(other => Mathf.Abs(other - e) < 0.02f) > 1).ToList();

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var lookup = new Dictionary<Vector3Int, int>();
        int Weld(Vector3 p)
        {
            var key = Vector3Int.RoundToInt(p * 1000f);
            if (!lookup.TryGetValue(key, out int index))
            {
                index = vertices.Count;
                lookup[key] = index;
                vertices.Add(p);
            }
            return index;
        }

        for (int p = 0; p < pieces.Length; p++)
        {
            var points = piecePoints[p];
            var indexes = pieces[p].GetComponent<MeshFilter>().sharedMesh.triangles;
            for (int i = 0; i < indexes.Length; i += 3)
            {
                Vector3 a = points[indexes[i]], b = points[indexes[i + 1]], c = points[indexes[i + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-8f)
                    continue;
                normal.Normalize();
                if (normal.y < -0.5f)
                    continue; // dessous, posé sur la route
                if (Mathf.Abs(normal.z) > 0.99f && seams.Any(s => Mathf.Abs(a.z - s) < 0.02f))
                    continue; // bout collé à la pièce voisine
                triangles.Add(Weld(a));
                triangles.Add(Weld(b));
                triangles.Add(Weld(c));
            }
        }
        SplitTJunctions(vertices, triangles);

        var mesh = new Mesh { name = root.name + " Collider" };
        mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();

        var collider = root.GetComponent<MeshCollider>();
        if (collider == null)
            collider = Undo.AddComponent<MeshCollider>(root);
        Undo.RecordObject(collider, "Collider de l'îlot");
        collider.sharedMesh = mesh;

        // Layer Floor + tag Obstacle comme les trottoirs : PlayerCollision fait crasher / StepUp contre les bords
        Undo.RecordObject(root, "Collider de l'îlot");
        root.layer = pieces[0].gameObject.layer;
        root.tag = "Obstacle";
    }

    // Coupe les triangles dont une arête passe par un vertex d'une autre pièce (colonnes qui ne tombent
    // pas pile d'une pièce à l'autre) : sans ça l'arête n'est pas partagée et reste "accrochante"
    static void SplitTJunctions(List<Vector3> vertices, List<int> triangles)
    {
        for (int t = 0; t < triangles.Count; t += 3)
        {
            for (int e = 0; e < 3; e++)
            {
                int a = triangles[t + e], b = triangles[t + (e + 1) % 3], c = triangles[t + (e + 2) % 3];
                int split = FindVertexOnEdge(vertices, a, b);
                if (split < 0)
                    continue;

                // (a, split, c) remplace le triangle, (split, b, c) part à la fin : même sens d'enroulement
                triangles[t] = a;
                triangles[t + 1] = split;
                triangles[t + 2] = c;
                triangles.Add(split);
                triangles.Add(b);
                triangles.Add(c);
                t -= 3; // on revérifie le triangle raccourci
                break;
            }
        }
    }

    static int FindVertexOnEdge(List<Vector3> vertices, int a, int b)
    {
        Vector3 start = vertices[a], edge = vertices[b] - start;
        float lengthSq = edge.sqrMagnitude;
        for (int i = 0; i < vertices.Count; i++)
        {
            if (i == a || i == b)
                continue;
            float s = Vector3.Dot(vertices[i] - start, edge) / lengthSq;
            if (s > 0.001f && s < 0.999f && (start + edge * s - vertices[i]).sqrMagnitude < 1e-6f)
                return i;
        }
        return -1;
    }
}
