using System;
using System.Collections.Generic;
using UnityEngine;

public enum TutorialPhase
{
    Phase1_WASD,
    Phase2_Vertical,
    Phase3_Full
}

public enum TutorialState
{
    Idle,
    Running,
    Transitioning,
    Completed
}

public class TutorialPhaseConfig
{
    [Header("阶段名称")]
    public TutorialPhase phase;

    [Header("显示")]
    public string phaseName;
    public string hintText;
    public string[] ringHintTexts;

    [Header("关联的光圈序号")]
    public List<int> ringIndices;
}

public class TutorialManager : MonoBehaviour
{
    [Header("阶段配置")]
    [SerializeField] TutorialPhaseConfig phase1Config;
    [SerializeField] TutorialPhaseConfig phase2Config;
    [SerializeField] TutorialPhaseConfig phase3Config;

    [Header("引用")]
    [SerializeField] TutorialRing[] allRings;
    [SerializeField] TutorialHud hud;
    [SerializeField] TutorialInputRestriction inputRestriction;
    [SerializeField] SystemDialogController systemDialog;

    [Header("系统对话语音（可选）")]
    [SerializeField] AudioClip introVoiceClip;
    [SerializeField] AudioClip phase2VoiceClip;
    [SerializeField] AudioClip phase3VoiceClip;
    [SerializeField] AudioClip completedVoiceClip;

    [Header("阶段过渡等待时间")]
    [SerializeField] float transitionDelay = 2.5f;

    TutorialPhase _currentPhase = TutorialPhase.Phase1_WASD;
    TutorialState _state = TutorialState.Idle;
    int _passedRingCount;
    HashSet<int> _passedRingSet;
    float _transitionTimer;

    TutorialPhaseConfig CurrentConfig
    {
        get
        {
            return _currentPhase switch
            {
                TutorialPhase.Phase1_WASD => phase1Config,
                TutorialPhase.Phase2_Vertical => phase2Config,
                TutorialPhase.Phase3_Full => phase3Config,
                _ => null
            };
        }
    }

    public TutorialPhase CurrentPhase => _currentPhase;
    public TutorialState State => _state;

    void Awake()
    {
        BuildDefaultConfigs();
    }

    void Start()
    {
        if (hud == null) hud = FindObjectOfType<TutorialHud>();
        if (inputRestriction == null) inputRestriction = FindObjectOfType<TutorialInputRestriction>();
        if (systemDialog == null) systemDialog = FindObjectOfType<SystemDialogController>();

        SubscribeToRings();
        StartTutorial();
    }

    void Update()
    {
        if (_state == TutorialState.Transitioning)
        {
            _transitionTimer -= Time.deltaTime;
            if (_transitionTimer <= 0f)
                CompleteTransition();
        }
    }

    void OnDestroy()
    {
        UnsubscribeFromRings();
    }

    void BuildDefaultConfigs()
    {
        if (phase1Config == null)
            phase1Config = new TutorialPhaseConfig
            {
                phase = TutorialPhase.Phase1_WASD,
                phaseName = "第一阶段：基础悬停与平移",
                hintText = "使用 WASD 移动无人机",
                ringHintTexts = new[] { "前", "左", "右" },
                ringIndices = new List<int> { 0, 1, 2 }
            };

        if (phase2Config == null)
            phase2Config = new TutorialPhaseConfig
            {
                phase = TutorialPhase.Phase2_Vertical,
                phaseName = "第二阶段：垂直空间与高度控制",
                hintText = "使用 空格上升 / Ctrl 下降",
                ringHintTexts = new[] { "上", "下" },
                ringIndices = new List<int> { 3, 4 }
            };

        if (phase3Config == null)
            phase3Config = new TutorialPhaseConfig
            {
                phase = TutorialPhase.Phase3_Full,
                phaseName = "第三阶段：完整赛道",
                hintText = "通过所有光圈，金色光圈需按住方向键蓄满加速",
                ringHintTexts = new[] { "①", "②", "③", "④", "⑤" },
                ringIndices = new List<int> { 5, 6, 7, 8, 9 }
            };
    }

    void SubscribeToRings()
    {
        foreach (var ring in allRings)
        {
            if (ring != null)
                ring.OnPassed += OnRingPassed;
        }
    }

    void UnsubscribeFromRings()
    {
        foreach (var ring in allRings)
        {
            if (ring != null)
                ring.OnPassed -= OnRingPassed;
        }
    }

    public void StartTutorial()
    {
        Debug.Log("[Tutorial] StartTutorial 被调用");
        _currentPhase = TutorialPhase.Phase1_WASD;
        _state = TutorialState.Running;
        _passedRingCount = 0;
        _passedRingSet = new HashSet<int>();

        ApplyInputRestriction();
        ActivateRingsForPhase();
        Debug.Log($"[Tutorial] StartTutorial，hud={(hud != null ? "非空" : "null")}，config ringIndices={CurrentConfig?.ringIndices?.Count}");
        hud?.ShowPhase(CurrentConfig);
        PlayDialogForPhase(_currentPhase, false);
    }

    void ApplyInputRestriction()
    {
        switch (_currentPhase)
        {
            case TutorialPhase.Phase1_WASD:
                inputRestriction?.SetRestriction(false, false, false);
                break;
            case TutorialPhase.Phase2_Vertical:
                inputRestriction?.SetRestriction(true, false, false);
                break;
            case TutorialPhase.Phase3_Full:
                inputRestriction?.SetRestriction(true, true, true);
                break;
        }
    }

    void ActivateRingsForPhase()
    {
        var indices = CurrentConfig?.ringIndices;
        if (indices == null) return;

        Debug.Log($"[Tutorial] ActivateRingsForPhase，激活光圈: [{string.Join(", ", indices)}]，总数={indices.Count}");

        foreach (var ring in allRings)
        {
            if (ring != null)
            {
                bool active = indices.Contains(ring.ringIndex);
                ring.gameObject.SetActive(active);
                if (active) ring.ResetRing();
            }
        }
    }

    void OnRingPassed(int ringIndex)
    {
        Debug.Log($"[Tutorial] OnRingPassed({ringIndex})，当前状态={_state}");

        if (_state != TutorialState.Running) return;

        var indices = CurrentConfig?.ringIndices;
        if (indices == null || !indices.Contains(ringIndex))
        {
            Debug.LogWarning($"[Tutorial] 光圈 {ringIndex} 不在当前阶段，配置的索引: [{string.Join(", ", indices)}]");
            return;
        }

        if (_passedRingSet.Contains(ringIndex)) return;
        _passedRingSet.Add(ringIndex);
        _passedRingCount++;

        Debug.Log($"[Tutorial] 通过光圈 {ringIndex}，进度 {_passedRingCount}/{indices.Count}");
        hud?.UpdateProgress(_passedRingCount, indices.Count);

        if (_passedRingCount >= indices.Count)
            AdvanceToNextPhase();
    }

    void AdvanceToNextPhase()
    {
        Debug.Log($"[Tutorial] AdvanceToNextPhase，_currentPhase={_currentPhase}");
        _state = TutorialState.Transitioning;
        _transitionTimer = transitionDelay;

        if (_currentPhase == TutorialPhase.Phase3_Full)
        {
            CompleteTutorial();
            return;
        }

        string nextPhaseName = _currentPhase switch
        {
            TutorialPhase.Phase1_WASD => phase2Config?.phaseName ?? "第二阶段",
            TutorialPhase.Phase2_Vertical => phase3Config?.phaseName ?? "第三阶段",
            _ => "下一阶段"
        };

        hud?.ShowPhaseTransition(nextPhaseName);
    }

    void CompleteTransition()
    {
        Debug.Log($"[Tutorial] CompleteTransition");

        _currentPhase = _currentPhase switch
        {
            TutorialPhase.Phase1_WASD => TutorialPhase.Phase2_Vertical,
            TutorialPhase.Phase2_Vertical => TutorialPhase.Phase3_Full,
            _ => _currentPhase
        };

        _passedRingCount = 0;
        _passedRingSet = new HashSet<int>();
        _state = TutorialState.Running;

        ApplyInputRestriction();
        ActivateRingsForPhase();
        Debug.Log($"[Tutorial] CompleteTransition，hud={(hud != null ? "非空" : "null")}，config ringIndices={CurrentConfig?.ringIndices?.Count}");
        hud?.ShowPhase(CurrentConfig);
        PlayDialogForPhase(_currentPhase, true);
    }

    void CompleteTutorial()
    {
        _state = TutorialState.Completed;
        inputRestriction?.SetRestriction(true, true, true);
        hud?.ShowCompleted();
        PlayCompleteDialog();
    }

    void PlayDialogForPhase(TutorialPhase phase, bool fromTransition)
    {
        if (systemDialog == null)
            return;

        string lineText = phase switch
        {
            TutorialPhase.Phase1_WASD => "系统提示：第一阶段开始，使用 WASD 移动无人机。",
            TutorialPhase.Phase2_Vertical => "系统提示：第二阶段开始，使用空格上升与 Ctrl 下降。",
            TutorialPhase.Phase3_Full => "系统提示：第三阶段开始，按顺序通过全部光圈。",
            _ => "系统提示：阶段开始。"
        };

        if (fromTransition)
            lineText = "系统提示：准备进入下一阶段。 " + lineText;

        var voice = phase switch
        {
            TutorialPhase.Phase1_WASD => introVoiceClip,
            TutorialPhase.Phase2_Vertical => phase2VoiceClip,
            TutorialPhase.Phase3_Full => phase3VoiceClip,
            _ => null
        };

        systemDialog.PlayDialog(new List<SystemDialogLine>
        {
            new() { text = lineText, voiceClip = voice, characterInterval = 0.035f }
        });
    }

    void PlayCompleteDialog()
    {
        if (systemDialog == null)
            return;

        systemDialog.PlayDialog(new List<SystemDialogLine>
        {
            new() { text = "系统提示：教学完成，祝你执行任务顺利。", voiceClip = completedVoiceClip, characterInterval = 0.035f }
        });
    }

    /// <summary>重新开始教学关卡</summary>
    public void RestartTutorial()
    {
        foreach (var ring in allRings)
        {
            if (ring != null)
                ring.ResetRing();
        }
        StartTutorial();
    }
}
