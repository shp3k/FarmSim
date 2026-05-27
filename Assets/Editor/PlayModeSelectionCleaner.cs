using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeSelectionCleaner
{
    private static bool cleanupQueued;

    static PlayModeSelectionCleaner()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        Selection.selectionChanged += QueueClearNullSelection;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode
            || state == PlayModeStateChange.EnteredEditMode
            || state == PlayModeStateChange.EnteredPlayMode)
        {
            QueueClearSelection();
        }
    }

    private static void QueueClearNullSelection()
    {
        Object[] selected = Selection.objects;
        if (selected == null || selected.Length == 0)
        {
            return;
        }

        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] == null)
            {
                QueueClearSelection();
                return;
            }
        }
    }

    private static void QueueClearSelection()
    {
        if (cleanupQueued)
        {
            return;
        }

        cleanupQueued = true;
        EditorApplication.delayCall += ClearSelection;
    }

    private static void ClearSelection()
    {
        cleanupQueued = false;
        Selection.objects = System.Array.Empty<Object>();
    }
}
