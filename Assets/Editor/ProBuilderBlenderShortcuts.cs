using System;
using System.Linq;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.ProBuilder;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.ProBuilder;

// Raccourcis ProBuilder façon Blender, rebindables dans Edit > Shortcuts > ProBuilder :
// Tab = GameObject mode <-> ProBuilder mode, puis en ProBuilder mode : 1/2/3 = vertex/edge/face, E = extrude.
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
}
