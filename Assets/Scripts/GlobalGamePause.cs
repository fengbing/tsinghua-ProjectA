using UnityEngine;

/// <summary>
/// 与 <see cref="GameUi"/> 的 Esc 菜单联动：打开窗口时暂停，关闭时恢复。
/// <see cref="Time.timeScale"/> = 0 且 <see cref="AudioListener.pause"/> = true。
/// </summary>
public static class GlobalGamePause
{
    static bool _paused;
    static float _savedTimeScale = 1f;

    public static bool IsPaused => _paused;

    public static void Toggle()
    {
        if (_paused)
            Resume();
        else
            Pause();
    }

    public static void Pause()
    {
        if (_paused)
            return;
        _savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        _paused = true;
    }

    public static void Resume()
    {
        if (!_paused)
            return;
        AudioListener.pause = false;
        Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
        _paused = false;
    }

    /// <summary>离开小游戏等流程时确保恢复，避免残留静音/停表。</summary>
    public static void ForceResumeIfPaused() => Resume();
}
