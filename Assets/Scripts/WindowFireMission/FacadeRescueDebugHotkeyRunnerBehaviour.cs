#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// 常驻监听 9 键打开立面救援调试流程；不挂在立面 Canvas 上，避免该物体未激活时 Update 不跑。
/// 调试完成后可删除本脚本（及 .meta）。
/// </summary>
[DefaultExecutionOrder(32000)]
sealed class FacadeRescueDebugHotkeyRunnerBehaviour : MonoBehaviour
{
    static bool _bootstrapped;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _bootstrapped = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild)
            return;
#endif
        if (_bootstrapped)
            return;
        _bootstrapped = true;
        var host = new GameObject("[Debug] FacadeRescueHotkey9");
        DontDestroyOnLoad(host);
        host.AddComponent<FacadeRescueDebugHotkeyRunnerBehaviour>();
    }

    void Update()
    {
        if (!Application.isPlaying)
            return;
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild)
            return;
#endif
        if (!Input.GetKeyDown(KeyCode.Alpha9) && !Input.GetKeyDown(KeyCode.Keypad9))
            return;

        var list = FindObjectsByType<FacadeRescueMiniGameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var ctrl = list.Length > 0 ? list[0] : null;
        if (ctrl == null)
        {
            Debug.LogWarning(
                "[FacadeRescueDebug] 未找到 FacadeRescueMiniGameController（含未激活物体）。请确认当前场景包含立面小游戏（如 Level 2）。");
            return;
        }

        ctrl.TryDebugOpenFacadeMinigame();
    }
}
#endif
