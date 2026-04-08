using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 场景进入时触发系统对话，可选整局只触发一次。
/// 支持单句兼容模式，也支持多句顺序播放（例如 7 段语音）。
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
    [Space(8)]
    [Tooltip("多句模式：按列表顺序播放。为空时回退到上面的单句配置。")]
    [SerializeField] List<SystemDialogLine> enterLines = new();

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
        if (systemDialog == null)
            return;

        List<SystemDialogLine> linesToPlay = BuildLinesToPlay();
        if (linesToPlay.Count == 0)
            return;
        systemDialog.ApplyTextStyle(enterFont, enterFontSize);
        systemDialog.PlayDialog(linesToPlay);

        if (oncePerGameRun && !string.IsNullOrWhiteSpace(GetKey()))
            PlayedKeys.Add(GetKey());
    }

    List<SystemDialogLine> BuildLinesToPlay()
    {
        List<SystemDialogLine> result = new();

        if (enterLines != null && enterLines.Count > 0)
        {
            foreach (var line in enterLines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.text))
                    continue;

                float interval = ResolveCharacterInterval(line.text, line.voiceClip, line.characterInterval);
                result.Add(new SystemDialogLine
                {
                    text = line.text,
                    voiceClip = line.voiceClip,
                    characterInterval = interval
                });
            }

            return result;
        }

        if (string.IsNullOrWhiteSpace(enterText))
            return result;

        result.Add(new SystemDialogLine
        {
            text = enterText,
            voiceClip = enterVoice,
            characterInterval = ResolveCharacterInterval(enterText, enterVoice, characterInterval)
        });

        return result;
    }

    float ResolveCharacterInterval(string text, AudioClip voice, float fallbackInterval)
    {
        float interval = fallbackInterval > 0f ? fallbackInterval : 0.04f;
        if (!autoFitTextToVoiceEnd || voice == null || string.IsNullOrEmpty(text))
            return interval;

        float totalDuration = voice.length + Mathf.Max(0f, extraSecondsAfterVoice);
        return Mathf.Max(0.005f, totalDuration / text.Length);
    }

    string GetKey()
    {
        if (!string.IsNullOrWhiteSpace(dedupeKey))
            return dedupeKey.Trim();
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "::SceneEntryDialogTrigger";
    }
}
