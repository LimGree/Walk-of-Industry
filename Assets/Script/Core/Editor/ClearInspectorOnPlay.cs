using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class ClearInspectorOnPlay
{
    static ClearInspectorOnPlay()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        EditorApplication.update += DropDeadSelection;
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode
            || state == PlayModeStateChange.EnteredPlayMode
            || state == PlayModeStateChange.ExitingPlayMode
            || state == PlayModeStateChange.EnteredEditMode)
            Selection.activeObject = null;
    }

    static void DropDeadSelection()
    {
        Object t = Selection.activeObject;
        if (ReferenceEquals(t, null))
            return;
        if (t)
            return;
        Selection.activeObject = null;
    }
}
