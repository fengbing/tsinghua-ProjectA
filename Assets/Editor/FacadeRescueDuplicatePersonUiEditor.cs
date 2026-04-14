#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 按 <see cref="FacadeRescueMiniGameController"/> 的 peoplePerWindow，从 w{i}_details 复制出 w{i}_p{j}_details（j≥1）。
/// 同窗 intro 共用 w{i}_intro，不复制 intro。
/// </summary>
public static class FacadeRescueDuplicatePersonUiEditor
{
    const string MenuPath = "Window Fire Mission/Facade/Duplicate extra person details from w*_details";

    [MenuItem(MenuPath)]
    static void DuplicateExtraPersonUi()
    {
        FacadeRescueMiniGameController ctrl = null;
        var found = Object.FindObjectsByType<FacadeRescueMiniGameController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (found != null && found.Length > 0)
            ctrl = found[0];
        if (ctrl == null)
        {
            EditorUtility.DisplayDialog("Facade", "当前场景未找到 FacadeRescueMiniGameController。", "OK");
            return;
        }

        var so = new SerializedObject(ctrl);
        var ppl = so.FindProperty("peoplePerWindow");
        int[] counts = { 2, 3, 2 };
        if (ppl != null && ppl.isArray && ppl.arraySize >= 3)
        {
            for (int i = 0; i < 3; i++)
                counts[i] = Mathf.Max(1, ppl.GetArrayElementAtIndex(i).intValue);
        }

        Transform root = ctrl.transform;
        var fullscreenProp = so.FindProperty("fullscreenRoot");
        if (fullscreenProp != null && fullscreenProp.objectReferenceValue is GameObject go && go != null)
            root = go.transform;
        int created = 0;
        Undo.SetCurrentGroupName("Duplicate facade person details UI");

        for (int wi = 0; wi < 3; wi++)
        {
            Transform detailsT = FindDescendantByNameRecursive(root, $"w{wi}_details");
            if (detailsT == null)
            {
                Debug.LogWarning($"[Facade] 窗 {wi} 缺少 w{wi}_details，跳过。");
                continue;
            }

            for (int pj = 1; pj < counts[wi]; pj++)
            {
                string detName = $"w{wi}_p{pj}_details";
                if (FindDescendantByNameRecursive(root, detName) != null)
                    continue;

                GameObject goD = Object.Instantiate(detailsT.gameObject, detailsT.parent);
                goD.name = detName;
                goD.SetActive(false);
                Undo.RegisterCreatedObjectUndo(goD, "Duplicate details");
                created++;
            }
        }

        Debug.Log($"[Facade] 已创建 {created} 个 details 物体。intro 共用 w*_intro；第一人仍用 w*_details。");
    }

    static Transform FindDescendantByNameRecursive(Transform t, string targetName)
    {
        if (t == null || string.IsNullOrEmpty(targetName))
            return null;
        if (string.Equals(t.name, targetName, System.StringComparison.OrdinalIgnoreCase))
            return t;
        for (int i = 0; i < t.childCount; i++)
        {
            Transform hit = FindDescendantByNameRecursive(t.GetChild(i), targetName);
            if (hit != null)
                return hit;
        }

        return null;
    }
}
#endif
