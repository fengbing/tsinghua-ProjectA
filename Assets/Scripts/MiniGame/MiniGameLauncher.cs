using UnityEngine;

/// <summary>在 Inspector 配置场景名，可由按钮 / UnityEvent 调用以 Additive 进入小游戏。</summary>
public class MiniGameLauncher : MonoBehaviour
{
    [Tooltip("Build Settings 中的场景名（不含 .unity），例如 BuiltinMiniGame")]
    [SerializeField] string additiveSceneName = "BuiltinMiniGame";

    [Tooltip("整局 timeScale=0；会连带停掉小游戏物理/动画。一般用关，靠流程里自动 Kinematic 冻无人机即可")]
    [SerializeField] bool pauseWorldTimeScale;

    public void Launch()
    {
        if (!MiniGameAdditiveFlow.Begin(additiveSceneName, pauseWorldTimeScale))
            Debug.LogWarning($"{nameof(MiniGameLauncher)}: Begin 失败，检查场景名与 Build Settings。", this);
    }
}
