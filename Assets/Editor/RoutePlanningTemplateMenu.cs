using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RoutePlanningTemplateMenu
{
    [MenuItem("Tools/Route Planning/Generate Segment Route Template (Active Scene)")]
    static void GenerateTemplateForActiveScene()
    {
        var controllers = Object.FindObjectsByType<RoutePlanningMiniGameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < controllers.Length; i++)
        {
            var c = controllers[i];
            if (c == null || !c.gameObject.scene.IsValid())
                continue;
            c.GenerateSegmentRouteTemplateInEditor();
            count++;
        }

        if (count > 0)
            EditorSceneManager.MarkSceneDirty(controllers[0].gameObject.scene);
        Debug.Log($"[RoutePlanningTemplateMenu] Generated segment templates for {count} controller(s).");
    }
}
