#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Adds a bottom distance HUD under the first scene Canvas. Uses <see cref="DevelopmentDistanceHudSource"/> by default;
/// enable <see cref="ForwardRaycastDistanceSource"/> and disable the dev source for forward ray distance.
/// </summary>
public static class DistanceHudMenu
{
    [MenuItem("GameObject/UI/Distance HUD Strip", false, 21)]
    static void CreateDistanceHudStrip()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[DistanceHud] No Canvas in the scene. Create a Canvas first.");
            return;
        }

        var go = new GameObject("DistanceHudStrip", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Distance HUD Strip");
        go.transform.SetParent(canvas.transform, false);
        var dev = go.AddComponent<DevelopmentDistanceHudSource>();
        dev.enabled = false;
        var forward = go.AddComponent<ForwardRaycastDistanceSource>();
        forward.enabled = false;
        go.AddComponent<DistanceToTargetSource>();
        go.AddComponent<DistanceHudView>();
        EditorUtility.SetDirty(go);
        Selection.activeGameObject = go;
    }
}
#endif
