using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 场景进入时触发一段系统对话，可选整局只触发一次。
/// </summary>
public class SceneEntryDialogTrigger : MonoBehaviour
{
    static readonly HashSet<string> PlayedKeys = new();

    [Header("Dialog")]
    [SerializeField] SystemDialogController systemDialog;
    [TextArea(2, 5)]
    [SerializeField] string enterText = "系统提示：欢迎进入场景。";
    [SerializeField] AudioClip enterVoice;
    [Min(0.005f)]
    [SerializeField] float characterInterval = 0.04f;
    [SerializeField] bool autoFitTextToVoiceEnd;
    [SerializeField] float extraSecondsAfterVoice = 1f;
    [SerializeField] TMP_FontAsset enterFont;
    [SerializeField] int enterFontSize;

    [Header("Play Control")]
    [SerializeField] float delaySeconds;
    [SerializeField] bool oncePerGameRun;
    [SerializeField] string dedupeKey;

    public void ConfigureDefaults(
        string text,
        AudioClip voice,
        float delay,
        TMP_FontAsset font,
        int fontSize,
        bool autoFitToVoice,
        float extraSeconds)
    {
        if (string.IsNullOrWhiteSpace(enterText))
            enterText = text;
        if (enterVoice == null)
            enterVoice = voice;
        if (delaySeconds <= 0f)
            delaySeconds = delay;
        if (enterFont == null)
            enterFont = font;
        if (enterFontSize <= 0)
            enterFontSize = fontSize;
        autoFitTextToVoiceEnd = autoFitToVoice;
        if (extraSecondsAfterVoice <= 0f)
            extraSecondsAfterVoice = extraSeconds;
    }

    void Start()
    {
        if (oncePerGameRun && !string.IsNullOrWhiteSpace(GetKey()) && PlayedKeys.Contains(GetKey()))
            return;

        if (delaySeconds > 0f)
            Invoke(nameof(PlayDialog), delaySeconds);
        else
            PlayDialog();
    }

    void PlayDialog()
    {
        if (systemDialog == null)
            systemDialog = FindObjectOfType<SystemDialogController>();
        if (systemDialog == null || string.IsNullOrWhiteSpace(enterText))
            return;
        systemDialog.ApplyTextStyle(enterFont, enterFontSize);

        float interval = characterInterval;
        if (autoFitTextToVoiceEnd && enterVoice != null && enterText.Length > 0)
        {
            float totalDuration = enterVoice.length + Mathf.Max(0f, extraSecondsAfterVoice);
            interval = Mathf.Max(0.005f, totalDuration / enterText.Length);
        }

        systemDialog.PlayDialog(new List<SystemDialogLine>
        {
            new() { text = enterText, voiceClip = enterVoice, characterInterval = interval }
        });

        if (oncePerGameRun && !string.IsNullOrWhiteSpace(GetKey()))
            PlayedKeys.Add(GetKey());
    }

    string GetKey()
    {
        if (!string.IsNullOrWhiteSpace(dedupeKey))
            return dedupeKey.Trim();
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "::SceneEntryDialogTrigger";
    }
}
