using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space prompt: F 键与台词各有一块独立背景（Image），便于换图。
/// </summary>
public class WindowFireDualPromptHud : MonoBehaviour
{
    [SerializeField] GameObject panelRoot;
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
        if (panelRoot != null)
            panelRoot.SetActive(false);
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
