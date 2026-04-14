using System;
using UnityEngine;

/// <summary>
/// 快递配送完整流程状态机中枢。
/// 协调楼栋外弹窗 → 楼顶验证 → 第一人称描边 → 阳台打开 → 投递完成的全流程。
/// </summary>
public class DeliveryPhaseManager : MonoBehaviour
{
    public static DeliveryPhaseManager Instance { get; private set; }

    public enum DeliveryState
    {
        Idle,
        DroneAtBuildingExterior,   // 到达楼栋外，等待2秒弹窗确认
        RequestRooftopVerify,      // 确认后，前往楼顶验证
        DroneAtRooftop,            // 进入楼顶层
        Verifying,                 // 验证中
        Verified,                  // 验证完成，等待飞回楼栋外
        DroneAtBuilding,           // 再次到达楼栋外，进入第一人称扫描
        DroneInFirstPerson,        // 第一人称中
        CrosshairOnTarget,         // 准星锁定目标阳台
        DroneApproachBalcony,      // 接近目标阳台
        BalconyOpened,             // 阳台已打开
        DeliveryComplete
    }

    [Header("UI")]
    [SerializeField] DeliveryPromptsUI promptsUI;
    [SerializeField] TutorialHud hud;

    [Header("叙事导演")]
    [SerializeField] PlaneGameNarrativeDirector narrativeDirector;

    [Header("子组件")]
    [SerializeField] RooftopVerifier rooftopVerifier;
    [SerializeField] BalconyOutlineEffect targetBalconyOutline;
    [SerializeField] CrosshairTargetDetector crosshairDetector;
    [SerializeField] FirstPersonModeListener firstPersonListener;

    [Header("场景物体")]
    [SerializeField] GameObject roofZone;
    [SerializeField] GameObject verificationRing;
    [SerializeField] GameObject targetBalcony;
    [SerializeField] GameObject balconyCollider;

    [Header("音效 - 阳台打开")]
    [Tooltip("按 E 键打开阳台时播放的音效（如机械展开音效）")]
    [SerializeField] AudioClip balconyOpenClip;
    [Tooltip("阳台打开音效的音量（0~1）")]
    [Range(0f, 1f)][SerializeField] float balconyOpenVolume = 1f;
    [Tooltip("阳台打开音效从第几秒开始播放")]
    [SerializeField] float balconyOpenStartTime = 0f;

    [Header("阳台动画")]
    [SerializeField] Animator balconyAnimator;
    [Tooltip("Animator Controller 中的状态名（区分大小写，如 \"骨骼|deploed\"）")]
    [SerializeField] string balconyOpenStateName = "deployed";
    [Tooltip("Animator Controller 中的 bool 参数名（区分大小写，如 \"IsOpen\"），用于 SetBool 方式")]
    [SerializeField] string balconyOpenParamName = "IsOpen";
    [SerializeField] float balconyColliderEnableDelay = 0.8f;

    [Header("Backup 场景专用")]
    [Tooltip("旁白4播放后由 BackupNarrativeController 设置为 true，替代 Animator 动画为 Z 轴插值")]
    [SerializeField] public bool useBackupScenePositionLerp;

    DeliveryState _currentState = DeliveryState.Idle;
    bool _flowStarted;
    AudioSource _audioSource;

    public DeliveryState CurrentState => _currentState;
    public bool IsFlowActive => _flowStarted && _currentState != DeliveryState.Idle && _currentState != DeliveryState.DeliveryComplete;

    public event Action<DeliveryState> OnStateChanged;
    public event Action OnDeliveryComplete;
    public event Action OnBalconyOpened;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;

        if (hud == null)
            hud = FindObjectOfType<TutorialHud>();
        if (promptsUI == null)
            promptsUI = FindObjectOfType<DeliveryPromptsUI>();
        if (narrativeDirector == null)
            narrativeDirector = FindObjectOfType<PlaneGameNarrativeDirector>();

        // 立即订阅所有子组件事件，保证即使流程未显式启动也能响应
        SubscribeToComponents();
    }

    void Start()
    {
        // 延迟到 Start 执行，确保场景中所有 MonoBehaviour.Awake() 都已运行完毕
        // 再强制将阳台 Animator 设置为收起状态（避免被其他组件的 WriteDefaultValues 覆盖）
        InitializeBalconyToStowed();
    }

    /// <summary>强制将阳台 Animator 设置为收起状态（骨骼|stowed）。</summary>
    void InitializeBalconyToStowed()
    {
        if (balconyAnimator == null) return;

        if (!string.IsNullOrEmpty(balconyOpenParamName))
        {
            balconyAnimator.SetBool(balconyOpenParamName, false);
            Debug.Log("[DeliveryPhaseManager] 阳台初始化为收起状态（SetBool IsOpen=false）");
        }
        else if (!string.IsNullOrEmpty(balconyOpenStateName))
        {
            balconyAnimator.Play(balconyOpenStateName, -1, 1f);
            Debug.Log("[DeliveryPhaseManager] 阳台初始化为收起状态（Play stowed at t=1.0）");
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeFromComponents();
    }

    void SubscribeToComponents()
    {
        if (rooftopVerifier != null)
        {
            rooftopVerifier.OnRooftopZoneEntered += HandleRooftopZoneEntered;
            rooftopVerifier.OnRingEntered += HandleRingEntered;
            rooftopVerifier.OnVerifyComplete += HandleVerifyComplete;
            rooftopVerifier.OnProgressUpdated += HandleProgressUpdated;
        }

        if (crosshairDetector != null)
        {
            crosshairDetector.OnTargetHit += HandleCrosshairHitTarget;
            crosshairDetector.OnTargetMissed += HandleCrosshairMissed;
        }

        if (firstPersonListener != null)
        {
            firstPersonListener.OnFirstPersonEnabled += NotifyFirstPersonEnabled;
        }
    }

    void UnsubscribeFromComponents()
    {
        if (rooftopVerifier != null)
        {
            rooftopVerifier.OnRooftopZoneEntered -= HandleRooftopZoneEntered;
            rooftopVerifier.OnRingEntered -= HandleRingEntered;
            rooftopVerifier.OnVerifyComplete -= HandleVerifyComplete;
            rooftopVerifier.OnProgressUpdated -= HandleProgressUpdated;
        }

        if (crosshairDetector != null)
        {
            crosshairDetector.OnTargetHit -= HandleCrosshairHitTarget;
            crosshairDetector.OnTargetMissed -= HandleCrosshairMissed;
        }

        if (firstPersonListener != null)
        {
            firstPersonListener.OnFirstPersonEnabled -= NotifyFirstPersonEnabled;
        }
    }

    /// <summary>外部入口：无人机到达目标楼栋附近时调用（如近距音效触发处）</summary>
    public void StartDeliveryFlow()
    {
        if (_flowStarted) return;
        _flowStarted = true;

        Debug.Log("[DeliveryPhaseManager] 配送流程启动");
        AdvanceTo(DeliveryState.DroneAtBuildingExterior);
    }

    void AdvanceTo(DeliveryState newState)
    {
        DeliveryState prev = _currentState;
        _currentState = newState;
        OnStateChanged?.Invoke(newState);

        switch (newState)
        {
            // === 阶段1：楼栋外弹窗 ===
            case DeliveryState.DroneAtBuildingExterior:
                break;

            // === 阶段2：前往楼顶验证 ===
            case DeliveryState.RequestRooftopVerify:
                ShowPrompt("请先前往楼顶进行验证");
                DisableBalconyCollider();
                break;

            case DeliveryState.DroneAtRooftop:
                ShowPrompt("飞入光圈，长按 E 键验证身份");
                break;

            case DeliveryState.Verifying:
                promptsUI?.ShowWithProgress("验证中，请保持按住 E 键...", 0f);
                break;

            // === 阶段3：验证完成，等待飞回楼栋外 ===
            case DeliveryState.Verified:
                ShowPrompt("身份验证成功！");
                // 延迟后自动推进到 DroneAtBuilding，让玩家飞回楼栋
                StartCoroutine(DelayThenAdvance(DeliveryState.DroneAtBuilding, 2f));
                break;

            // === 阶段4：楼栋外 → 第一人称扫描 ===
            case DeliveryState.DroneAtBuilding:
                ShowPrompt("请开启第一人称视角扫描");
                break;

            case DeliveryState.DroneInFirstPerson:
                ShowPrompt("请将准星对准目标接收阳台");
                // 描边由 CrosshairTargetDetector 接管：准星对准阳台时才显示
                break;

            case DeliveryState.CrosshairOnTarget:
                ShowPrompt("目标阳台已锁定，请前往投放");
                // 描边已在 DroneInFirstPerson 中开启，此处不再重复设置
                break;

            // === 阶段5：阳台打开投递 ===
            case DeliveryState.DroneApproachBalcony:
                ShowPrompt("请按 E 键打开阳台");
                // 到达阳台旁后关闭描边（无人机已在目标位置，不再需要视线引导）
                if (targetBalconyOutline != null)
                    targetBalconyOutline.SetOutlineEnabled(false);
                break;

            case DeliveryState.BalconyOpened:
                if (targetBalconyOutline != null)
                    targetBalconyOutline.SetOutlineEnabled(false);
                ShowPrompt("请按F键投递快递");
                PlayBalconyOpenSound();
                if (useBackupScenePositionLerp)
                    StartCoroutine(LerpBalconyZ(-0.742f, -0.075f, 3f));
                else
                    PlayBalconyOpenAnimation();
                StartCoroutine(EnableBalconyColliderDelayed(balconyColliderEnableDelay));
                OnBalconyOpened?.Invoke();
                break;

            case DeliveryState.DeliveryComplete:
                ShowPrompt("投递完成！");
                OnDeliveryComplete?.Invoke();
                if (narrativeDirector != null)
                    narrativeDirector.NotifyDeliveryComplete();
                break;
        }
    }

    System.Collections.IEnumerator DelayThenAdvance(DeliveryState next, float delay)
    {
        yield return new WaitForSeconds(delay);
        AdvanceTo(next);
    }

    void ShowPrompt(string text)
    {
        Debug.Log($"[DeliveryPhaseManager.ShowPrompt] text=\"{text}\" promptsUI={(promptsUI != null)} hud={(hud != null)}");
        if (promptsUI != null)
        {
            Debug.Log($"[DeliveryPhaseManager.ShowPrompt] 调用 promptsUI.ShowPrompt(\"{text}\")");
            promptsUI.ShowPrompt(text);
        }
        else if (hud != null)
        {
            if (string.IsNullOrEmpty(text))
                hud.HideAll();
            else
                hud.ShowPhase(CreateSimpleConfig(text));
        }
        else
        {
            Debug.LogWarning("[DeliveryPhaseManager.ShowPrompt] promptsUI 和 hud 都为 null，无法显示提示");
        }
    }

    TutorialPhaseConfig CreateSimpleConfig(string hint)
    {
        var cfg = new TutorialPhaseConfig
        {
            phaseName = "快递配送",
            hintText = hint,
            ringIndices = new System.Collections.Generic.List<int>()
        };
        return cfg;
    }

    // === 子组件事件处理 ===

    void HandleRooftopZoneEntered()
    {
        Debug.Log($"[DeliveryPhaseManager] HandleRooftopZoneEntered 被调用，当前状态: {_currentState}");
        if (_currentState != DeliveryState.RequestRooftopVerify)
        {
            Debug.LogWarning($"[DeliveryPhaseManager] HandleRooftopZoneEntered 被拒绝，期望 RequestRooftopVerify，实际 {_currentState}");
            return;
        }
        AdvanceTo(DeliveryState.DroneAtRooftop);
    }

    void HandleRingEntered()
    {
        Debug.Log($"[DeliveryPhaseManager] HandleRingEntered 被调用，当前状态: {_currentState}");
        if (_currentState != DeliveryState.DroneAtRooftop)
        {
            Debug.LogWarning($"[DeliveryPhaseManager] HandleRingEntered 被拒绝，期望 DroneAtRooftop，实际 {_currentState}");
            return;
        }
        AdvanceTo(DeliveryState.Verifying);
    }

    void HandleProgressUpdated(float progress)
    {
        Debug.Log($"[DeliveryPhaseManager] HandleProgressUpdated progress={progress:F2} 当前状态={_currentState}");
        if (_currentState == DeliveryState.Verifying)
        {
            promptsUI?.UpdateProgress(progress);
        }
    }

    void HandleVerifyComplete()
    {
        Debug.Log($"[DeliveryPhaseManager] HandleVerifyComplete 被调用，当前状态: {_currentState}");
        if (_currentState != DeliveryState.Verifying)
        {
            Debug.LogWarning($"[DeliveryPhaseManager] HandleVerifyComplete 被拒绝，期望 Verifying，实际 {_currentState}");
            return;
        }
        if (verificationRing != null) verificationRing.SetActive(false);
        AdvanceTo(DeliveryState.Verified);
    }

    void HandleCrosshairHitTarget()
    {
        if (_currentState == DeliveryState.DroneInFirstPerson)
            AdvanceTo(DeliveryState.CrosshairOnTarget);
    }

    void HandleCrosshairMissed()
    {
        if (_currentState == DeliveryState.CrosshairOnTarget)
            AdvanceTo(DeliveryState.DroneInFirstPerson);
    }

    // === 外部调用接口 ===

    /// <summary>由 DeliverySceneBridge 调用：楼栋外区域计时到达，弹出弹窗。</summary>
    public void TriggerBuildingDialogFromBridge()
    {
        Debug.Log($"[DeliveryPhaseManager] TriggerBuildingDialogFromBridge 当前状态: {_currentState}");
        if (_currentState != DeliveryState.DroneAtBuildingExterior)
        {
            Debug.LogWarning($"[DeliveryPhaseManager] 弹窗触发失败，期望 DroneAtBuildingExterior，实际 {_currentState}");
            return;
        }

        if (promptsUI != null)
        {
            Debug.Log("[DeliveryPhaseManager] 调用 promptsUI.ShowModalDialog");
            promptsUI.ShowModalDialog(
                "温馨提示",
                "请前往楼顶进行身份验证",
                () =>
                {
                    Debug.Log("[DeliveryPhaseManager] 弹窗已确认，回调触发，切换到 RequestRooftopVerify");
                    AdvanceTo(DeliveryState.RequestRooftopVerify);
                }
            );
        }
        else
        {
            Debug.LogWarning("[DeliveryPhaseManager] promptsUI 为空，直接切换状态");
            AdvanceTo(DeliveryState.RequestRooftopVerify);
        }
    }

    /// <summary>第一人称开启后由 FirstPersonModeListener 调用</summary>
    public void NotifyFirstPersonEnabled()
    {
        if (_currentState == DeliveryState.DroneAtBuilding)
        {
            if (crosshairDetector != null)
                crosshairDetector.enabled = true;
            AdvanceTo(DeliveryState.DroneInFirstPerson);
        }
    }

    /// <summary>无人机到达楼栋外区域时由 DeliverySceneBridge 调用</summary>
    public void NotifyDroneAtBuilding()
    {
        Debug.Log($"[DeliveryPhaseManager] NotifyDroneAtBuilding 当前状态: {_currentState}");

        if (_currentState == DeliveryState.Verified)
        {
            AdvanceTo(DeliveryState.DroneAtBuilding);
        }
        else if (_currentState == DeliveryState.RequestRooftopVerify)
        {
            Debug.Log("[DeliveryPhaseManager] 弹窗尚未确认，忽略此次进入楼栋外区域");
        }
        else if (_currentState == DeliveryState.DroneAtBuildingExterior)
        {
            Debug.Log("[DeliveryPhaseManager] 已在楼栋外，等待弹窗触发");
        }
        else if (_currentState == DeliveryState.Idle)
        {
            Debug.Log("[DeliveryPhaseManager] 配送未开始，切换到 DroneAtBuildingExterior");
            AdvanceTo(DeliveryState.DroneAtBuildingExterior);
        }
    }

    /// <summary>无人机到达阳台旁时由 DeliverySceneBridge 调用</summary>
    public void NotifyDroneNearBalcony()
    {
        Debug.Log($"[DeliveryPhaseManager] NotifyDroneNearBalcony 被调用，当前状态: {_currentState}");
        if (_currentState == DeliveryState.CrosshairOnTarget)
            AdvanceTo(DeliveryState.DroneApproachBalcony);
        else if (_currentState == DeliveryState.DroneInFirstPerson)
            AdvanceTo(DeliveryState.DroneApproachBalcony);
        else
            Debug.Log($"[DeliveryPhaseManager] NotifyDroneNearBalcony 状态不匹配，未切换状态");
    }

    /// <summary>按 G 打开阳台时由 DeliveryGKeyHandler 调用</summary>
    public void NotifyOpenBalcony()
    {
        Debug.Log($"[DeliveryPhaseManager] NotifyOpenBalcony 被调用，当前状态: {_currentState}");
        if (_currentState == DeliveryState.DroneApproachBalcony
            || _currentState == DeliveryState.CrosshairOnTarget
            || _currentState == DeliveryState.DroneInFirstPerson)
        {
            Debug.Log("[DeliveryPhaseManager] 状态匹配，开始切换到 BalconyOpened");
            AdvanceTo(DeliveryState.BalconyOpened);
        }
        else
        {
            Debug.Log("[DeliveryPhaseManager] 状态不匹配，未切换状态");
        }
    }

    /// <summary>阳台内包裹投递完成后由投递区调用</summary>
    public void NotifyDeliverySettled()
    {
        if (_currentState == DeliveryState.BalconyOpened)
            AdvanceTo(DeliveryState.DeliveryComplete);
    }

    void DisableBalconyCollider()
    {
        if (balconyCollider != null)
            balconyCollider.SetActive(false);
    }

    void EnableBalconyCollider()
    {
        if (balconyCollider != null)
            balconyCollider.SetActive(true);
    }

    void PlayBalconyOpenAnimation()
    {
        Debug.Log($"[DeliveryPhaseManager] PlayBalconyOpenAnimation，balconyAnimator={(balconyAnimator != null ? balconyAnimator.name : "null")}");
        if (balconyAnimator == null)
        {
            Debug.LogWarning("[DeliveryPhaseManager] 未指定 balconyAnimator，跳过阳台动画");
            return;
        }

        Debug.Log($"[DeliveryPhaseManager] balconyOpenParamName=\"{balconyOpenParamName}\"，balconyOpenStateName=\"{balconyOpenStateName}\"");
        if (!string.IsNullOrEmpty(balconyOpenParamName))
        {
            balconyAnimator.SetBool(balconyOpenParamName, true);
            Debug.Log($"[DeliveryPhaseManager] 阳台播放弹出动画（SetBool {balconyOpenParamName}=true）");
        }
        else if (!string.IsNullOrEmpty(balconyOpenStateName))
        {
            balconyAnimator.Play(balconyOpenStateName, -1, 0f);
            balconyAnimator.Update(0f);
            balconyAnimator.speed = 1f;
            Debug.Log($"[DeliveryPhaseManager] 阳台动画播放一次（State: {balconyOpenStateName}）");
        }
    }

    /// <summary>
    /// Backup 场景专用：将 targetBalcony 的 Z 轴在 duration 秒内从 startZ 匀速插值到 endZ。
    /// 使用 Time.unscaledDeltaTime，不受 Time.timeScale 影响。
    /// </summary>
    System.Collections.IEnumerator LerpBalconyZ(float startZ, float endZ, float duration = 3f)
    {
        if (targetBalcony == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = targetBalcony.transform.localPosition;
            pos.z = Mathf.Lerp(startZ, endZ, t);
            targetBalcony.transform.localPosition = pos;
            yield return null;
        }
        Vector3 finalPos = targetBalcony.transform.localPosition;
        finalPos.z = endZ;
        targetBalcony.transform.localPosition = finalPos;
    }

    System.Collections.IEnumerator EnableBalconyColliderDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        EnableBalconyCollider();
    }

    void PlayBalconyOpenSound()
    {
        if (balconyOpenClip == null || _audioSource == null) return;
        float clampedTime = Mathf.Clamp(balconyOpenStartTime, 0f, balconyOpenClip.length);
        _audioSource.clip = balconyOpenClip;
        _audioSource.time = clampedTime;
        _audioSource.volume = balconyOpenVolume;
        _audioSource.loop = false;
        _audioSource.Play();
    }

    /// <summary>第一人称是否开启（供外部检测）</summary>
    public bool IsFirstPersonActive()
    {
        if (crosshairDetector != null)
            return crosshairDetector.enabled;
        return false;
    }
}
