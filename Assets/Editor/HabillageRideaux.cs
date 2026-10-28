using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Habillage des rideaux de fer des façades : un shop (prefab) qui cache le rideau ou un graff (decal depuis un PNG).
// Shops dans Assets/Habillage/Shops : root sans rotation, pivot en bas au centre, dos contre le mur (z = 0), +Z vers la rue.
// Graffs dans Assets/Habillage/Graffs : simples PNG, le matériau decal est créé automatiquement.
static class Habillage
{
    public const string ShopsDir = "Assets/Habillage/Shops";
    public const string GraffsDir = "Assets/Habillage/Graffs";
    const string GraffMaterialsDir = GraffsDir + "/Materials";
    const string ChildName = "Habillage";

    // Marge pour que le shop déborde un peu du rideau et le cache entièrement
    const float ShopMargin = 1.04f;
    // Un shop peut être étiré en largeur, mais sa hauteur suit au moins à 70 % pour ne pas l'écraser
    const float ShopMinAspect = 0.7f;
    const float GraffMargin = 0.95f;
    // Boîte de projection du graff : devant le mur et derrière (rideaux légèrement en retrait)
    const float GraffFront = 0.3f, GraffDepth = 0.8f;

    public static List<GameObject> Shops() => Load<GameObject>(ShopsDir, "t:Prefab");
    public static List<Texture2D> Graffs() => Load<Texture2D>(GraffsDir, "t:Texture2D").Where(t => !AssetDatabase.GetAssetPath(t).StartsWith(GraffMaterialsDir)).ToList();

    static List<T> Load<T>(string dir, string filter) where T : Object =>
        !AssetDatabase.IsValidFolder(dir) ? new List<T>() :
        AssetDatabase.FindAssets(filter, new[] { dir }).Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<T>).Where(a => a != null).ToList();

    public static Transform Current(RideauDeFer rideau) => rideau.transform.Find(ChildName);

    public static void Clear(RideauDeFer rideau)
    {
        var current = Current(rideau);
        if (current != null)
            Undo.DestroyObjectImmediate(current.gameObject);
    }

    public static void Apply(RideauDeFer rideau, Object item)
    {
        Clear(rideau);
        if (item is GameObject shop)
            ApplyShop(rideau, shop);
        else if (item is Texture2D graff)
            ApplyGraff(rideau, graff);
    }

    // Taille du rideau dans le monde (la façade peut être étirée en X par le générateur)
    static Vector2 WorldSize(RideauDeFer rideau)
    {
        var s = rideau.transform.lossyScale;
        return new Vector2(rideau.size.x * Mathf.Abs(s.x), rideau.size.y * Mathf.Abs(s.y));
    }

    static void ApplyShop(RideauDeFer rideau, GameObject prefab)
    {
        var shop = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rideau.transform);
        Undo.RegisterCreatedObjectUndo(shop, "Habiller le rideau");
        shop.name = ChildName;
        shop.transform.localPosition = Vector3.zero;
        shop.transform.localRotation = Quaternion.identity;
        shop.transform.localScale = Vector3.one;

        // Le calcul se fait en taille monde puis est ramené dans l'espace (éventuellement étiré) du rideau
        var cover = Cover(prefab);
        var size = WorldSize(rideau);
        var parentScale = new Vector3(Mathf.Abs(rideau.transform.lossyScale.x), Mathf.Abs(rideau.transform.lossyScale.y), Mathf.Abs(rideau.transform.lossyScale.z));
        float sx = size.x / cover.width * ShopMargin;
        float sy = Mathf.Max(size.y / cover.height * ShopMargin, sx * ShopMinAspect);
        float sz = Mathf.Min(sx, sy);
        var scale = new Vector3(sx / parentScale.x, sy / parentScale.y, sz / parentScale.z);
        shop.transform.localScale = scale;
        // Devanture centrée sur le rideau, posée en bas
        shop.transform.localPosition = new Vector3(-cover.center.x * scale.x, -cover.yMin * scale.y, 0f);
    }

    // Zone de devanture à couvrir : ShopHabillage.cover, sinon toute la boîte du shop
    static Rect Cover(GameObject prefab)
    {
        var info = prefab.GetComponent<ShopHabillage>();
        if (info != null && info.cover.width > 0f && info.cover.height > 0f)
            return info.cover;
        var b = PrefabBounds(prefab);
        return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
    }

    // Boîte du shop à l'échelle 1 dans son propre espace
    static Bounds ShopBounds(GameObject shop)
    {
        var renderers = shop.GetComponentsInChildren<MeshRenderer>();
        var b = FacadePrefabSetup.LocalBounds(shop.transform, renderers[0]);
        foreach (var r in renderers.Skip(1))
            b.Encapsulate(FacadePrefabSetup.LocalBounds(shop.transform, r));
        // Le scale courant du shop est inclus dans worldToLocal : boîte déjà à l'échelle 1
        return b;
    }

    static void ApplyGraff(RideauDeFer rideau, Texture2D texture)
    {
        var go = new GameObject(ChildName);
        Undo.RegisterCreatedObjectUndo(go, "Habiller le rideau");
        go.transform.SetParent(rideau.transform, false);

        var size = WorldSize(rideau) * GraffMargin;
        float aspect = (float)texture.width / texture.height;
        // Calé en largeur si l'image est plus large que le rideau, sinon en hauteur
        var fitted = aspect > size.x / size.y ? new Vector2(size.x, size.x / aspect) : new Vector2(size.y * aspect, size.y);

        var parentScale = rideau.transform.lossyScale;
        go.transform.localPosition = new Vector3(0f, rideau.size.y / 2, GraffFront / Mathf.Abs(parentScale.z));
        // Le projecteur projette selon son +Z : tourné vers le mur
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var projector = go.AddComponent<DecalProjector>();
        projector.material = GraffMaterial(texture);
        projector.scaleMode = DecalScaleMode.ScaleInvariant;
        projector.size = new Vector3(fitted.x, fitted.y, GraffDepth);
        projector.pivot = new Vector3(0f, 0f, GraffDepth / 2);
        projector.drawDistance = 1000f;
    }

    // Un matériau decal par PNG, créé une fois dans Graffs/Materials
    static Material GraffMaterial(Texture2D texture)
    {
        var path = $"{GraffMaterialsDir}/{texture.name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
        if (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        if (!AssetDatabase.IsValidFolder(GraffMaterialsDir))
            AssetDatabase.CreateFolder(GraffsDir, "Materials");
        material = new Material(Shader.Find("Shader Graphs/Decal"));
        material.SetTexture("Base_Map", texture);
        material.enableInstancing = true;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Habillage aléatoire : shop, graff ou rien. Les shops dont les proportions collent au rideau sont favorisés
    public static void Randomize(RideauDeFer rideau, System.Random random, float shopChance, float graffChance, List<GameObject> shops, List<Texture2D> graffs)
    {
        double roll = random.NextDouble();
        if (roll < shopChance && shops.Count > 0)
        {
            var size = WorldSize(rideau);
            var weights = shops.Select(s =>
            {
                var c = Cover(s);
                float stretch = Mathf.Abs(Mathf.Log(size.x / c.width * c.height / size.y));
                return Mathf.Exp(-2f * stretch);
            }).ToList();
            Apply(rideau, shops[Pick(random, weights)]);
        }
        else if (roll < shopChance + graffChance && graffs.Count > 0)
            Apply(rideau, graffs[random.Next(graffs.Count)]);
        else
            Clear(rideau);
    }

    static readonly Dictionary<GameObject, Bounds> prefabBounds = new Dictionary<GameObject, Bounds>();

    static Bounds PrefabBounds(GameObject prefab)
    {
        if (!prefabBounds.TryGetValue(prefab, out var b))
            prefabBounds[prefab] = b = ShopBounds(prefab);
        return b;
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

    // Gizmo cliquable dans la Scene view pour sélectionner un rideau
    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Pickable)]
    static void DrawRideau(RideauDeFer rideau, GizmoType type)
    {
        bool selected = (type & GizmoType.Selected) != 0;
        Gizmos.matrix = rideau.transform.localToWorldMatrix;
        var center = new Vector3(0f, rideau.size.y / 2, 0.05f);
        var size = new Vector3(rideau.size.x, rideau.size.y, 0.02f);
        Gizmos.color = selected ? new Color(1f, 0.6f, 0f, 0.35f) : new Color(1f, 0.6f, 0f, 0.08f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = selected ? new Color(1f, 0.8f, 0f) : new Color(1f, 0.6f, 0f, 0.6f);
        Gizmos.DrawWireCube(center, size);
    }
}

class HabillageRideaux : EditorWindow
{
    float shopChance = 0.45f, graffChance = 0.35f;
    Vector2 scroll;
    List<GameObject> shops;
    List<Texture2D> graffs;

    [MenuItem("FOUVELIB/Habillage des rideaux")]
    static void Open() => GetWindow<HabillageRideaux>("Habillage rideaux");

    void OnEnable() => Refresh();
    void OnFocus() => Refresh();
    void OnSelectionChange() => Repaint();

    void Refresh()
    {
        shops = Habillage.Shops();
        graffs = Habillage.Graffs();
    }

    // Rideaux visés : ceux sélectionnés, ou tous ceux des façades / pâtés sélectionnés
    static List<RideauDeFer> Targets() => Selection.gameObjects
        .SelectMany(g => g.GetComponentsInChildren<RideauDeFer>(true)).Distinct().ToList();

    void OnGUI()
    {
        var targets = Targets();
        EditorGUILayout.HelpBox(targets.Count == 0
            ? "Sélectionne des rideaux (cadres orange dans la Scene view), une façade ou un pâté entier."
            : $"{targets.Count} rideau(x) sélectionné(s)", targets.Count == 0 ? MessageType.Info : MessageType.None);

        using (new EditorGUI.DisabledScope(targets.Count == 0))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Vider"))
                ForEach(targets, "Vider les rideaux", Habillage.Clear);
            if (GUILayout.Button("Aléatoire"))
            {
                var random = new System.Random();
                ForEach(targets, "Habillage aléatoire", r => Habillage.Randomize(r, random, shopChance, graffChance, shops, graffs));
            }
            EditorGUILayout.EndHorizontal();
            shopChance = EditorGUILayout.Slider("Chance shop", shopChance, 0f, 1f);
            graffChance = EditorGUILayout.Slider("Chance graff", graffChance, 0f, 1f - shopChance);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        Grid("Shops", Habillage.ShopsDir, shops, targets);
        Grid("Graffs", Habillage.GraffsDir, graffs, targets);
        EditorGUILayout.EndScrollView();
    }

    void Grid<T>(string title, string dir, List<T> items, List<RideauDeFer> targets) where T : Object
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"{title}  ({dir})", EditorStyles.boldLabel);
        if (items.Count == 0)
        {
            EditorGUILayout.HelpBox("Dossier vide", MessageType.None);
            return;
        }
        const float cell = 96f;
        int columns = Mathf.Max(1, (int)((position.width - 20) / (cell + 4)));
        for (int i = 0; i < items.Count; i += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + columns, items.Count); j++)
            {
                var item = items[j];
                var preview = AssetPreview.GetAssetPreview(item) ?? AssetPreview.GetMiniThumbnail(item);
                if (preview == null || AssetPreview.IsLoadingAssetPreview(item.GetEntityId()))
                    Repaint();
                EditorGUILayout.BeginVertical(GUILayout.Width(cell));
                using (new EditorGUI.DisabledScope(targets.Count == 0))
                {
                    if (GUILayout.Button(new GUIContent(preview, item.name), GUILayout.Width(cell), GUILayout.Height(cell)))
                        ForEach(targets, "Habiller les rideaux", r => Habillage.Apply(r, item));
                }
                GUILayout.Label(item.name, EditorStyles.miniLabel, GUILayout.Width(cell));
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    static void ForEach(List<RideauDeFer> targets, string undoName, System.Action<RideauDeFer> action)
    {
        Undo.SetCurrentGroupName(undoName);
        int group = Undo.GetCurrentGroup();
        foreach (var rideau in targets)
            action(rideau);
        Undo.CollapseUndoOperations(group);
    }
}
