using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 教学关卡 HUD 界面。
/// 所有 UI 元素均通过 Inspector 拖入，脚本只负责显示/隐藏和内容更新。
/// </summary>
public class TutorialHud : MonoBehaviour
{
    [Header("阶段名称 UI")]
    [SerializeField] TextMeshProUGUI phaseNameText;
    [SerializeField] Image phaseNameBg;

    [Header("操作提示 UI")]
    [SerializeField] TextMeshProUGUI hintText;
    [SerializeField] Image hintBg;

    [Header("进度提示 UI")]
    [SerializeField] TextMeshProUGUI progressText;
    [SerializeField] Image progressBg;

    [Header("反馈文字")]
    [SerializeField] TextMeshProUGUI feedbackText;

    [Header("反馈文字显示时长")]
    [SerializeField] float feedbackDisplayDuration = 1.2f;

    float _feedbackTimer;
    bool _narrationActive;
    TutorialPhaseConfig _currentConfig;

    void Update()
    {
        if (_feedbackTimer > 0f)
        {
            _feedbackTimer -= Time.deltaTime;
            if (_feedbackTimer <= 0f)
                HideFeedback();
        }
    }

    /// <summary>显示当前阶段信息</summary>
    public void ShowPhase(TutorialPhaseConfig config)
    {
        if (config == null) return;

        // 缓存当前配置，旁白结束后用于恢复
        _currentConfig = config;

        if (phaseNameText != null)
        {
            phaseNameText.text = config.phaseName;
            phaseNameText.gameObject.SetActive(true);
        }
        if (phaseNameBg != null) phaseNameBg.gameObject.SetActive(true);

        // 旁白播放期间不显示 hint，等待旁白结束后再显示
        if (_narrationActive)
        {
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (hintBg != null) hintBg.gameObject.SetActive(false);
        }
        else
        {
            if (hintText != null)
            {
                hintText.text = config.hintText;
                hintText.gameObject.SetActive(true);
            }
            if (hintBg != null) hintBg.gameObject.SetActive(true);
        }

        if (progressBg != null)
        {
            progressBg.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[TutorialHud] progressBg 未赋值，请在 Inspector 中拖入");
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(true);
            progressText.text = $"光圈 0 / {config.ringIndices.Count}";
            Debug.Log($"[TutorialHud] ShowPhase 设置进度: '光圈 0 / {config.ringIndices.Count}'，gameObject.activeSelf={progressText.gameObject.activeSelf}");
        }
        else
        {
            Debug.LogWarning("[TutorialHud] progressText 未赋值，请在 Inspector 中拖入");
        }

        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    /// <summary>旁白开始播放时调用：隐藏 hintText，避免遮挡旁白字幕</summary>
    public void SetNarrationActive(bool active)
    {
        _narrationActive = active;
        if (!active && _currentConfig != null)
        {
            // 旁白结束，恢复 hintText 显示
            if (hintText != null)
            {
                hintText.text = _currentConfig.hintText;
                hintText.gameObject.SetActive(true);
            }
            if (hintBg != null) hintBg.gameObject.SetActive(true);
        }
        else if (active)
        {
            if (hintText != null) hintText.gameObject.SetActive(false);
            if (hintBg != null) hintBg.gameObject.SetActive(false);
        }
    }

    /// <summary>更新进度（当前 / 总数）</summary>
    public void UpdateProgress(int current, int total)
    {
        if (progressText != null)
            progressText.text = $"光圈 {current} / {total}";
    }

    /// <summary>显示阶段过渡提示</summary>
    public void ShowPhaseTransition(string nextPhaseName)
    {
        if (phaseNameText != null)
        {
            phaseNameText.text = nextPhaseName;
            phaseNameText.gameObject.SetActive(true);
        }

        if (hintText != null) hintText.gameObject.SetActive(false);
        if (hintBg != null) hintBg.gameObject.SetActive(false);
        if (progressText != null)
        {
            progressText.gameObject.SetActive(false);
            Debug.Log($"[TutorialHud] ShowPhaseTransition 隐藏 progressText，progressBg={progressBg != null}");
        }
        if (progressBg != null) progressBg.gameObject.SetActive(false);
    }

    /// <summary>通过光圈时短暂显示反馈</summary>
    public void ShowRingPassed()
    {
        if (feedbackText != null)
        {
            feedbackText.text = "通过!";
            feedbackText.gameObject.SetActive(true);
        }
        _feedbackTimer = feedbackDisplayDuration;
    }

    /// <summary>提示需要加速</summary>
    public void ShowBoostRequired()
    {
        if (feedbackText != null)
        {
            feedbackText.text = "请蓄满加速!";
            feedbackText.gameObject.SetActive(true);
        }
        _feedbackTimer = feedbackDisplayDuration;
    }

    /// <summary>提示顺序错误</summary>
    public void ShowWrongOrder()
    {
        if (feedbackText != null)
        {
            feedbackText.text = "请按顺序通过光圈";
            feedbackText.gameObject.SetActive(true);
        }
        _feedbackTimer = feedbackDisplayDuration;
    }

    /// <summary>教学完成</summary>
    public void ShowCompleted()
    {
        if (phaseNameText != null) phaseNameText.text = "教学完成!";
        if (hintText != null) hintText.text = "恭喜你掌握了所有操作!";
        if (progressText != null) progressText.gameObject.SetActive(false);
        if (progressBg != null) progressBg.gameObject.SetActive(false);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    void HideFeedback()
    {
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }

    /// <summary>隐藏所有 HUD 元素</summary>
    public void HideAll()
    {
        if (phaseNameText != null) phaseNameText.gameObject.SetActive(false);
        if (phaseNameBg != null) phaseNameBg.gameObject.SetActive(false);
        if (hintText != null) hintText.gameObject.SetActive(false);
        if (hintBg != null) hintBg.gameObject.SetActive(false);
        if (progressText != null) progressText.gameObject.SetActive(false);
        if (progressBg != null) progressBg.gameObject.SetActive(false);
        if (feedbackText != null) feedbackText.gameObject.SetActive(false);
    }
}
