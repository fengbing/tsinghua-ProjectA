using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 构造 <see cref="SystemDialogLine"/> 与打字速度工具，便于任意脚本用「一句代码」走系统黑底对话框。
/// </summary>
public static class SystemDialogCue
{
    public static SystemDialogLine Line(string text, AudioClip voice, float characterInterval = 0.04f)
    {
        return new SystemDialogLine
        {
            text = text ?? string.Empty,
            voiceClip = voice,
            characterInterval = Mathf.Max(0.005f, characterInterval)
        };
    }

    /// <summary>与 <see cref="SceneEntryDialogTrigger"/> 一致：把整句打字时长摊到语音长度（+ 留白）。</summary>
    public static float CharacterIntervalToMatchVoice(string text, AudioClip voice, float extraSecondsAfterVoice = 1f)
    {
        if (voice == null || string.IsNullOrEmpty(text))
            return 0.04f;
        float totalDuration = voice.length + Mathf.Max(0f, extraSecondsAfterVoice);
        return Mathf.Max(0.005f, totalDuration / text.Length);
    }

    internal static List<SystemDialogLine> ToSingletonList(this SystemDialogLine line)
    {
        return new List<SystemDialogLine> { line };
    }
}
