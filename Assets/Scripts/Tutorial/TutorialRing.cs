using UnityEngine;
using System;

/// <summary>
/// 教学关卡光圈。当无人机以足够速度通过触发区时视为"通过"。
/// </summary>
public class TutorialRing : MonoBehaviour
{
    public event Action<int> OnPassed;

    [Header("配置")]
    [Tooltip("光圈序号，从0开始")]
    public int ringIndex;

    [Tooltip("所属阶段：0=基础悬停，1=垂直控制，2=完整赛道")]
    public int phase;

    [Tooltip("是否必须方向键蓄满加速（约 2 秒）才能通过")]
    public bool requiresBoost;

    [Tooltip("通过的最小速度阈值")]
    public float minPassSpeed = 2f;

    [Header("视觉反馈")]
    public Color normalColor = new Color(0.2f, 0.6f, 1f, 0.6f);
    public Color passedColor = new Color(0.2f, 1f, 0.4f, 0.8f);
    public Color boostRequiredColor = new Color(1f, 0.85f, 0.1f, 0.8f);

    [Header("音效")]
    public AudioSource audioSource;
    public AudioClip passSound;
    public AudioClip boostRequiredSound;

    [Header("通过后禁用时间（秒）")]
    public float disableDuration = 0.8f;

    bool _passed;
    bool _invoked;
    float _disableTimer;
    Renderer[] _renderers;
    Color _originalEmission;

    void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        if (_renderers.Length > 0)
        {
            _originalEmission = _renderers[0].material.HasProperty("_EmissionColor")
                ? _renderers[0].material.GetColor("_EmissionColor")
                : Color.black;
        }

        if (requiresBoost)
            SetVisualColor(boostRequiredColor);
        else
            SetVisualColor(normalColor);
    }

    void Update()
    {
        if (_disableTimer > 0f)
        {
            _disableTimer -= Time.deltaTime;
            if (_disableTimer <= 0f && _passed)
                gameObject.SetActive(false);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Ring {ringIndex}] OnTriggerEnter with {other.name}");
        if (_passed) return;
        if (_invoked) return;

        var plane = other.GetComponentInParent<PlaneController>();
        if (plane == null)
        {
            Debug.LogWarning($"[Ring {ringIndex}] 未找到 PlaneController，碰撞体: {other.name}");
            return;
        }

        bool fullAcceleration = plane.IsFullMovementAccelerationActive();

        if (requiresBoost && !fullAcceleration)
        {
            TutorialHud hud = FindObjectOfType<TutorialHud>();
            hud?.ShowBoostRequired();

            if (audioSource != null && boostRequiredSound != null)
                audioSource.PlayOneShot(boostRequiredSound);

            return;
        }

        _passed = true;
        _disableTimer = disableDuration;
        SetVisualColor(passedColor);

        if (audioSource != null && passSound != null)
            audioSource.PlayOneShot(passSound);

        TutorialHud hud2 = FindObjectOfType<TutorialHud>();
        hud2?.ShowRingPassed();

        _invoked = true;
        OnPassed?.Invoke(ringIndex);
    }

    void SetVisualColor(Color color)
    {
        foreach (var r in _renderers)
        {
            if (r.material.HasProperty("_Color"))
                r.material.SetColor("_Color", color);

            if (r.material.HasProperty("_EmissionColor"))
            {
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", color * 0.5f);
            }
        }
    }

    /// <summary>重置光圈状态（重新开始教学时调用）</summary>
    public void ResetRing()
    {
        _passed = false;
        _invoked = false;
        _disableTimer = 0f;
        gameObject.SetActive(true);

        if (requiresBoost)
            SetVisualColor(boostRequiredColor);
        else
            SetVisualColor(normalColor);
    }
}
