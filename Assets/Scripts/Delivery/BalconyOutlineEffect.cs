using EPOOutline;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 阳台描边：使用 Easy performant outline 的 <see cref="Outlinable"/>。
/// Built-in 管线需在主相机上有 <see cref="Outliner"/>；URP/HDRP 需在 Renderer Data 里加插件自带 Feature。
/// </summary>
[RequireComponent(typeof(Collider))]
public class BalconyOutlineEffect : MonoBehaviour
{
    [Header("描边颜色")]
    [SerializeField] Color outlineColor = Color.yellow;

    [Header("描边粗细（0~1，对应插件 DilateShift）")]
    [SerializeField] [Range(0.05f, 1f)] float outlineWidth = 0.35f;

    [Header("是否自动获取子物体 Mesh/Skinned 的 Renderer")]
    [SerializeField] bool autoAddRenderers = true;

    Outlinable _outlinable;

    void Awake()
    {
        EnsureOutlinable();
    }

    void OnEnable()
    {
        EnsureOutlinable();
        // 不在这里重设 Outlinable.enabled，由业务显式调用 SetOutlineEnabled 控制
    }

    void OnDisable()
    {
        // 不改动 Outlinable.enabled，由业务显式 Show/Hide
    }

    void OnDestroy()
    {
        if (_outlinable != null)
        {
            Destroy(_outlinable);
            _outlinable = null;
        }
    }

    void EnsureOutlinable()
    {
        if (_outlinable != null) return;

        _outlinable = GetComponent<Outlinable>();
        if (_outlinable == null)
            _outlinable = gameObject.AddComponent<Outlinable>();

        if (autoAddRenderers)
        {
            var field = typeof(Outlinable).GetField("outlineTargets",
                BindingFlags.Instance | BindingFlags.NonPublic);
            ((List<OutlineTarget>)field.GetValue(_outlinable)).Clear();

            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                if (r is not MeshRenderer && r is not SkinnedMeshRenderer)
                    continue;
                _outlinable.AddRenderer(r);
            }
        }

        _outlinable.RenderStyle = RenderStyle.Single;
        _outlinable.OutlineParameters.Color = outlineColor;
        _outlinable.OutlineParameters.DilateShift = Mathf.Clamp01(outlineWidth);
        _outlinable.OutlineParameters.BlurShift = 1f;

        // 去掉前景填充层，只保留轮廓（与 null Material 分支一致）
        _outlinable.OutlineParameters.FillPass.Shader = null;

        _outlinable.enabled = false;

        if (Application.isPlaying)
            EnsureBuiltInOutlinerOnMainCamera();

        Debug.Log($"[BalconyOutlineEffect] Outlinable 就绪，目标数 = {_outlinable.OutlineTargetsCount}");
    }

    static void EnsureBuiltInOutlinerOnMainCamera()
    {
        if (RenderPipelineManager.currentPipeline != null)
        {
            Debug.LogWarning(
                "[BalconyOutlineEffect] 当前为 SRP，相机上的 Built-in Outliner 不会执行。请在 URP Renderer Data 中添加插件的 Outline Renderer Feature。");
            return;
        }

        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[BalconyOutlineEffect] 未找到 Camera.main，无法自动添加 Outliner；请在主相机上挂载 EPOOutline.Outliner。");
            return;
        }

        if (cam.GetComponent<Outliner>() == null)
            cam.gameObject.AddComponent<Outliner>();
    }

    public void SetOutlineEnabled(bool enabled)
    {
        if (_outlinable == null) EnsureOutlinable();
        if (_outlinable != null)
            _outlinable.enabled = enabled;
    }

    public void SetOutlineColor(Color color)
    {
        outlineColor = color;
        if (_outlinable != null)
            _outlinable.OutlineParameters.Color = color;
    }

    public void SetOutlineWidth(float width)
    {
        outlineWidth = Mathf.Clamp01(width);
        if (_outlinable != null)
            _outlinable.OutlineParameters.DilateShift = outlineWidth;
    }

    public void ShowOutline() => SetOutlineEnabled(true);
    public void HideOutline() => SetOutlineEnabled(false);
}
