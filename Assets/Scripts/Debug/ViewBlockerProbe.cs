using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 运行时探测「屏幕中心」挡住了什么：先 UI（EventSystem 射线），再 3D Physics，再 2D Physics2D。
/// 挂任意物体上，运行后按快捷键（默认 F9）或在 Inspector 右键组件选 Probe Now。
/// </summary>
public class ViewBlockerProbe : MonoBehaviour
{
    [SerializeField] KeyCode probeKey = KeyCode.F9;
    [Tooltip("为空则用 Camera.main")]
    [SerializeField] Camera probeCamera;
    [SerializeField] int maxUiHitsToLog = 25;
    [SerializeField] int max3DHitsToLog = 20;

    void Update()
    {
        if (Input.GetKeyDown(probeKey))
            RunProbe();
    }

    [ContextMenu("Probe Now")]
    public void RunProbe()
    {
        var cam = probeCamera != null ? probeCamera : Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[ViewBlockerProbe] 无可用 Camera（probeCamera 与 Camera.main 皆空）。", this);
            return;
        }

        var screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        var sb = new StringBuilder();
        sb.AppendLine($"[ViewBlockerProbe] 屏幕中心 ({screenPos.x:F0},{screenPos.y:F0}) 相机={cam.name}");

        LogUiHits(screenPos, sb);
        Log3DHits(cam, screenPos, sb);
        Log2DHits(cam, screenPos, sb);
        LogEnabledOverlayCanvases(sb);

        Debug.Log(sb.ToString(), this);
    }

    void LogUiHits(Vector2 screenPos, StringBuilder sb)
    {
        var es = EventSystem.current;
        if (es == null)
        {
            sb.AppendLine("UI: EventSystem.current 为空");
            return;
        }

        var ped = new PointerEventData(es) { position = screenPos };
        var results = new List<RaycastResult>();
        es.RaycastAll(ped, results);
        int n = Mathf.Min(results.Count, maxUiHitsToLog);
        sb.AppendLine($"UI 射线命中数: {results.Count}（最多打印 {n}）");
        for (int i = 0; i < n; i++)
        {
            var r = results[i];
            var go = r.gameObject;
            var canvas = go.GetComponentInParent<Canvas>();
            string canvasInfo = canvas != null
                ? $"Canvas[{canvas.name}] order={canvas.sortingOrder} mode={canvas.renderMode}"
                : "无 Canvas 父级";
            sb.AppendLine(
                $"  [{i}] {HierarchyPath(go.transform)} | depth={r.depth} dist={r.distance:F1} | {canvasInfo}");
        }
    }

    void Log3DHits(Camera cam, Vector2 screenPos, StringBuilder sb)
    {
        var ray = cam.ScreenPointToRay(screenPos);
        var hits = Physics.RaycastAll(ray, 5000f, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        int n = Mathf.Min(hits.Length, max3DHitsToLog);
        sb.AppendLine($"3D RaycastAll: {hits.Length}（最多打印 {n}）");
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            var go = h.collider.gameObject;
            var sr = go.GetComponent<SpriteRenderer>();
            string extra = sr != null ? $" SpriteRenderer sort={sr.sortingLayerName}/{sr.sortingOrder}" : "";
            sb.AppendLine(
                $"  [{i}] {HierarchyPath(go.transform)} dist={h.distance:F2} layer={LayerMask.LayerToName(go.layer)}{extra}");
        }
    }

    void Log2DHits(Camera cam, Vector2 screenPos, StringBuilder sb)
    {
        float enter = 0f;
        var plane = new Plane(cam.transform.forward, cam.transform.position);
        var ray = cam.ScreenPointToRay(screenPos);
        if (!plane.Raycast(ray, out enter))
            enter = Mathf.Abs(cam.transform.position.z);
        if (enter < 0.01f)
            enter = 10f;
        Vector3 w3 = ray.GetPoint(enter);
        var pt = new Vector2(w3.x, w3.y);
        var cols = Physics2D.OverlapPointAll(pt);
        sb.AppendLine($"2D OverlapPointAll @ world ({pt.x:F2},{pt.y:F2}): {cols.Length}");
        const int cap = 15;
        for (int i = 0; i < cols.Length && i < cap; i++)
        {
            var go = cols[i].gameObject;
            sb.AppendLine($"  [{i}] {HierarchyPath(go.transform)} layer={LayerMask.LayerToName(go.layer)}");
        }
    }

    void LogEnabledOverlayCanvases(StringBuilder sb)
    {
        var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        sb.AppendLine($"当前启用的 Canvas 数: {canvases.Length}（Overlay 且 enabled）");
        foreach (var c in canvases)
        {
            if (c == null || !c.enabled || c.renderMode != RenderMode.ScreenSpaceOverlay)
                continue;
            sb.AppendLine($"  Overlay: {HierarchyPath(c.transform)} sortOrder={c.sortingOrder}");
        }
    }

    static string HierarchyPath(Transform t)
    {
        if (t == null)
            return "(null)";
        var stack = new Stack<string>();
        for (var x = t; x != null; x = x.parent)
            stack.Push(x.name);
        return string.Join("/", stack);
    }
}
