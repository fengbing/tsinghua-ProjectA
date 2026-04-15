using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 与 <see cref="SystemDialogController"/> / <see cref="SystemDialogController2"/> 对齐的对外 API，
/// 便于在 Inspector 中用 <see cref="Component"/> 引用并在运行时解析。
/// </summary>
public interface ISystemDialogPresentation
{
    void PlaySingleLine(string text, AudioClip voiceClip = null, float characterInterval = 0.04f);

    CustomYieldInstruction WaitUntilDialogIdle();

    void ApplyTextStyle(TMP_FontAsset overrideFont, int overrideFontSize = 0);

    void PlayDialog(IList<SystemDialogLine> lines);
}
