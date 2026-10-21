using System;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ProBuilder;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;

// Raccourcis ProBuilder façon Blender, rebindables dans Edit > Shortcuts > ProBuilder :
// Tab = GameObject mode <-> ProBuilder mode, puis en ProBuilder mode : 1/2/3 = vertex/edge/face, E = extrude, Ctrl+R = loop cut, X = delete, F = make face.
[InitializeOnLoad]
static class ProBuilderBlenderShortcuts
{
    // Le contexte ProBuilder est interne au package, on le récupère par son nom
    static readonly Type ProBuilderContext = Type.GetType("UnityEditor.ProBuilder.PositionToolContext, Unity.ProBuilder.Editor");

    // Actif seulement en ProBuilder mode avec la Scene view focus : 1/2/3/E restent libres ailleurs
    class ProBuilderModeContext : IShortcutContext
    {
        public bool active => IsInProBuilderMode && EditorWindow.focusedWindow is SceneView;
    }

    static bool IsInProBuilderMode => ProBuilderContext != null && ToolManager.activeContextType == ProBuilderContext;

    static ProBuilderBlenderShortcuts()
    {
        ShortcutManager.RegisterContext(new ProBuilderModeContext());
    }

    [Shortcut("ProBuilder/Toggle GameObject-ProBuilder Mode", typeof(SceneView), KeyCode.Tab)]
    static void ToggleMode()
    {
        if (ProBuilderContext == null)
        {
            Debug.LogWarning("ProBuilderBlenderShortcuts : contexte ProBuilder introuvable (version du package changée ?)");
            return;
        }

        if (IsInProBuilderMode)
        {
            ToolManager.SetActiveContext<GameObjectToolContext>();
            return;
        }

        if (!Selection.gameObjects.Any(go => go.GetComponent<ProBuilderMesh>() != null))
        {
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.ShowNotification(new GUIContent("Sélectionne un objet ProBuilder"), 1f);
            return;
        }
        ToolManager.SetActiveContext(ProBuilderContext);
    }

    [Shortcut("ProBuilder/Blender/Vertex Mode", typeof(ProBuilderModeContext), KeyCode.Alpha1)]
    static void VertexMode() => ProBuilderEditor.selectMode = SelectMode.Vertex;

    [Shortcut("ProBuilder/Blender/Edge Mode", typeof(ProBuilderModeContext), KeyCode.Alpha2)]
    static void EdgeMode() => ProBuilderEditor.selectMode = SelectMode.Edge;

    [Shortcut("ProBuilder/Blender/Face Mode", typeof(ProBuilderModeContext), KeyCode.Alpha3)]
    static void FaceMode() => ProBuilderEditor.selectMode = SelectMode.Face;

    // Extrude les faces ou les arêtes sélectionnées selon le mode (même action que Ctrl+E)
    [Shortcut("ProBuilder/Blender/Extrude", typeof(ProBuilderModeContext), KeyCode.E)]
    static void Extrude() => EditorApplication.ExecuteMenuItem("Tools/ProBuilder/Geometry/Extrude");

    // Loop cut sur les arêtes sélectionnées (même action que Alt+U). Prioritaire sur le Ctrl+R global (Refresh) en ProBuilder mode
    [Shortcut("ProBuilder/Blender/Loop Cut", typeof(ProBuilderModeContext), KeyCode.R, ShortcutModifiers.Action)]
    static void LoopCut() => EditorApplication.ExecuteMenuItem("Tools/ProBuilder/Geometry/Insert Edge Loop");

    // Comme le "Delete Vertices/Edges/Faces" de Blender : supprime les faces qui touchent la sélection.
    // ProBuilder n'a que Delete Faces (Backspace), un mesh ne peut pas garder de vertex sans face.
    [Shortcut("ProBuilder/Blender/Delete", typeof(ProBuilderModeContext), KeyCode.X)]
    static void Delete()
    {
        var meshes = MeshSelection.top.ToArray();
        if (meshes.Length == 0)
            return;

        Undo.RegisterCompleteObjectUndo(meshes, "Delete");
        foreach (var mesh in meshes)
        {
            var faces = FacesTouchingSelection(mesh);
            if (faces.Length == 0)
                continue;

            mesh.DeleteFaces(faces);
            mesh.ClearSelection();
            mesh.ToMesh();
            mesh.Refresh();
            mesh.Optimize();
        }
        ProBuilderEditor.Refresh();
    }

    // Comme le F de Blender : 2 arêtes -> bridge, sinon une face qui relie les vertices sélectionnés (traitée comme convexe)
    [Shortcut("ProBuilder/Blender/Make Face", typeof(ProBuilderModeContext), KeyCode.F)]
    static void MakeFace()
    {
        var meshes = MeshSelection.top.ToArray();
        if (meshes.Length == 0)
            return;

        Undo.RegisterCompleteObjectUndo(meshes, "Make Face");
        foreach (var mesh in meshes)
        {
            Face face;
            var edges = mesh.selectedEdges;
            if (ProBuilderEditor.selectMode == SelectMode.Edge && edges.Count == 2)
                face = mesh.Bridge(edges[0], edges[1]);
            else
            {
                var verts = ProBuilderEditor.selectMode == SelectMode.Edge
                    ? edges.SelectMany(e => new[] { e.a, e.b }).Distinct().ToList()
                    : mesh.selectedVertices.ToList();
                if (verts.Count < 3)
                    continue;
                face = mesh.CreatePolygon(verts, true);
            }

            if (face == null)
                continue;

            mesh.SetSelectedFaces(new[] { face });
            mesh.ToMesh();
            mesh.Refresh();
            mesh.Optimize();
        }
        ProBuilderEditor.Refresh();
    }

    static Face[] FacesTouchingSelection(ProBuilderMesh mesh)
    {
        if (ProBuilderEditor.selectMode == SelectMode.Face)
            return mesh.GetSelectedFaces();

        // Index du groupe de vertices coïncidents (un "vrai" vertex Blender) pour chaque index de vertex
        var shared = new int[mesh.vertexCount];
        for (int i = 0; i < mesh.sharedVertices.Count; i++)
            foreach (var v in mesh.sharedVertices[i])
                shared[v] = i;

        if (ProBuilderEditor.selectMode == SelectMode.Edge)
        {
            var edges = mesh.selectedEdges.Select(e => SharedEdge(shared, e)).ToHashSet();
            return mesh.faces.Where(f => f.edges.Any(e => edges.Contains(SharedEdge(shared, e)))).ToArray();
        }

        var verts = mesh.selectedVertices.Select(v => shared[v]).ToHashSet();
        return mesh.faces.Where(f => f.distinctIndexes.Any(v => verts.Contains(shared[v]))).ToArray();
    }

    static (int, int) SharedEdge(int[] shared, Edge e)
    {
        int a = shared[e.a], b = shared[e.b];
        return a < b ? (a, b) : (b, a);
    }
}
