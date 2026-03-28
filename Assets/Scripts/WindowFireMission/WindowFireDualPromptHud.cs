using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space prompt: F 键与台词各有一块独立背景（Image），便于换图。
/// 布局：改 <see cref="layoutRootRect"/> / 子物体 <see cref="keyBlockRect"/>、<see cref="instructionBlockRect"/> 与 <see cref="rowLayoutGroup"/>。
/// </summary>
public class WindowFireDualPromptHud : MonoBehaviour
{
    [SerializeField] GameObject panelRoot;
    [Tooltip("整块 UI 根；调 Anchors、Pos(X,Y)、Width/Height 可移动与缩放整体")]
    [SerializeField] RectTransform layoutRootRect;
    [Tooltip("F 键区域：改 Width/Height、LayoutElement 的 Preferred Width 等")]
    [SerializeField] RectTransform keyBlockRect;
    [Tooltip("台词区域：常与 Flexible Width 配合拉满剩余空间")]
    [SerializeField] RectTransform instructionBlockRect;
    [Tooltip("水平排列：Spacing=两框间距；Padding=整体内边距")]
    [SerializeField] HorizontalLayoutGroup rowLayoutGroup;
    [Min(0f)]
    [SerializeField] float rowSpacing = 14f;

    [Tooltip("F 键下方的背景图（可拖入 Sprite）")]
    [SerializeField] Image keyHintBackground;
    [SerializeField] TMP_Text keyHintText;
    [Tooltip("台词下方的背景图（可拖入 Sprite）")]
    [SerializeField] Image instructionBackground;
    [SerializeField] TMP_Text instructionText;

    [Header("Copy (defaults match mission spec)")]
    [SerializeField] string keyLabel = "F";
    [SerializeField] string sprinklerInstruction = "快打开喷淋系统灭火！";
    [SerializeField] string thermalInstruction = "快启用双光热成像相机！";

    public static WindowFireDualPromptHud Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        WireLayoutReferences();
        ApplyRowSpacing();
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    void OnValidate()
    {
        ApplyRowSpacing();
    }

    void WireLayoutReferences()
    {
        if (panelRoot == null)
            return;
        if (layoutRootRect == null)
            layoutRootRect = panelRoot.GetComponent<RectTransform>();
        if (keyBlockRect == null)
        {
            var t = panelRoot.transform.Find("KeyBlock");
            if (t != null)
                keyBlockRect = t as RectTransform;
        }

        if (instructionBlockRect == null)
        {
            var t = panelRoot.transform.Find("InstructionBlock");
            if (t != null)
                instructionBlockRect = t as RectTransform;
        }

        if (rowLayoutGroup == null)
            rowLayoutGroup = panelRoot.GetComponent<HorizontalLayoutGroup>();
    }

    void ApplyRowSpacing()
    {
        if (rowLayoutGroup != null)
            rowLayoutGroup.spacing = rowSpacing;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowSprinklerPrompt()
    {
        if (keyHintText != null)
            keyHintText.text = keyLabel;
        if (instructionText != null)
            instructionText.text = sprinklerInstruction;
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void ShowThermalPrompt()
    {
        if (keyHintText != null)
            keyHintText.text = keyLabel;
        if (instructionText != null)
            instructionText.text = thermalInstruction;
        if (panelRoot != null)
            panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }
}
