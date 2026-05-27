using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class SideShopPanelEditorAutoAttach
{
    static SideShopPanelEditorAutoAttach()
    {
        EditorApplication.delayCall += EnsureAttachedOnActiveScene;
        EditorSceneManager.sceneOpened += (_, __) => EnsureAttachedOnActiveScene();
    }

    private static void EnsureAttachedOnActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "MainScene")
        {
            return;
        }

        FarmUIManager uiManager = Object.FindFirstObjectByType<FarmUIManager>();
        if (uiManager == null)
        {
            return;
        }

        SideShopPanelController controller = Object.FindFirstObjectByType<SideShopPanelController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<SideShopPanelController>(uiManager.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        controller.EditorRebuildPreview();
    }
}
