using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 楼顶身份验证系统。
/// 管理楼顶层触发区、验证光圈的显示，以及长按 F 3秒的验证进度。
/// </summary>
public class RooftopVerifier : MonoBehaviour
{
    [Header("场景引用")]
    [Tooltip("楼顶层触发区（需有 Trigger Collider）")]
    [SerializeField] Collider roofZone;
    [Tooltip("验证光圈（初始 inactive）")]
    [SerializeField] GameObject verificationRing;
    [Tooltip("无人机（或其 Rigidbody 所在对象）")]
    [SerializeField] Transform drone;

    [Header("验证参数")]
    [Tooltip("长按 F 键验证所需时间（秒）")]
    [SerializeField] float verificationDuration = 3f;
    [Tooltip("无人机需进入光圈中心的半径（米）")]
    [SerializeField] float ringRadius = 3f;

    [Header("进度条 UI（可不填，使用内置 Debug）")]
    [Tooltip("用于显示验证进度的 Radial Fill Image（如 Image.fillAmount）")]
    [SerializeField] Image progressFillImage;

    [Header("音效 - 验证中")]
    [Tooltip("长按 E 键验证过程中循环播放的音效（如扫描/读条音效）")]
    [SerializeField] AudioClip verifyingClip;
    [Tooltip("验证中音效的音量（0~1）")]
    [Range(0f, 1f)][SerializeField] float verifyingVolume = 1f;
    [Tooltip("验证中音效从第几秒开始播放")]
    [SerializeField] float verifyingStartTime = 0f;

    [Header("音效 - 验证成功")]
    [Tooltip("验证成功时播放的音效（如成功提示音）")]
    [SerializeField] AudioClip verifySuccessClip;
    [Tooltip("验证成功音效的音量（0~1）")]
    [Range(0f, 1f)][SerializeField] float verifySuccessVolume = 1f;
    [Tooltip("验证成功音效从第几秒开始播放")]
    [SerializeField] float verifySuccessStartTime = 0f;

    public event Action OnRooftopZoneEntered;
    public event Action OnRingEntered;
    public event Action OnVerifyComplete;
    public event Action<float> OnProgressUpdated;

    bool _droneInsideRoofZone;
    bool _droneInsideRing;
    float _verifyProgress;
    bool _alreadyCompleted;
    bool _ringShown;
    AudioSource _verifyingAudioSource;
    bool _verifyingSoundStarted;

    public float VerifyProgress => _verifyProgress;
    public bool IsCompleted => _alreadyCompleted;

    void Awake()
    {
        if (verificationRing != null)
            verificationRing.SetActive(false);

        if (roofZone == null)
            Debug.LogWarning("[RooftopVerifier] roofZone 未赋值！");
        if (drone == null)
            Debug.LogWarning("[RooftopVerifier] drone 未赋值！");

        _verifyingAudioSource = gameObject.AddComponent<AudioSource>();
        _verifyingAudioSource.playOnAwake = false;
        _verifyingAudioSource.loop = true;
    }

    void Update()
    {
        if (_alreadyCompleted) return;

        CheckDroneInRing();

        if (_droneInsideRing)
        {
            if (Input.GetKey(KeyCode.E))
            {
                _verifyProgress += Time.deltaTime;
                UpdateProgressUI(_verifyProgress / verificationDuration);
                TryStartVerifyingSound();

                if (_verifyProgress >= verificationDuration)
                {
                    StopVerifyingSound();
                    CompleteVerification();
                }
            }
            else
            {
                StopVerifyingSound();
                UpdateProgressUI(_verifyProgress / verificationDuration);
            }
        }
    }

    void CheckDroneInRing()
    {
        if (drone == null || verificationRing == null)
        {
            if (drone == null) Debug.LogWarning("[RooftopVerifier] drone 为 null，无法检测距离！");
            if (verificationRing == null) Debug.LogWarning("[RooftopVerifier] verificationRing 为 null，无法检测距离！");
            return;
        }

        float dist = Vector3.Distance(drone.position, verificationRing.transform.position);
        bool nowInside = dist <= ringRadius;

        if (nowInside && !_droneInsideRing)
        {
            _droneInsideRing = true;
            Debug.Log("[RooftopVerifier] 无人机进入验证光圈，触发 OnRingEntered");
            OnRingEntered?.Invoke();
        }
        else if (!nowInside && _droneInsideRing)
        {
            _droneInsideRing = false;
            Debug.Log("[RooftopVerifier] 无人机离开验证光圈");
        }

        // 无人机能进入光圈但不在里面时，打印当前状态
        if (!nowInside && !_droneInsideRing)
        {
            // 近距离但未进入时不打印日志，避免刷屏
        }
    }

    void UpdateProgressUI(float fill)
    {
        float clampedFill = Mathf.Clamp01(fill);
        if (progressFillImage != null)
        {
            progressFillImage.fillAmount = clampedFill;
        }
        OnProgressUpdated?.Invoke(clampedFill);
    }

    void CompleteVerification()
    {
        if (_alreadyCompleted) return;
        _alreadyCompleted = true;

        Debug.Log("[RooftopVerifier] 身份验证完成！");

        if (progressFillImage != null)
            progressFillImage.fillAmount = 1f;

        PlayVerifySuccessSound();
        OnVerifyComplete?.Invoke();
    }

    void TryStartVerifyingSound()
    {
        if (verifyingClip == null || _verifyingAudioSource == null) return;
        if (_verifyingSoundStarted) return;

        _verifyingSoundStarted = true;
        PlayVerifyingSound();
    }

    void PlayVerifyingSound()
    {
        if (verifyingClip == null || _verifyingAudioSource == null) return;
        float clampedTime = Mathf.Clamp(verifyingStartTime, 0f, verifyingClip.length);
        _verifyingAudioSource.clip = verifyingClip;
        _verifyingAudioSource.time = clampedTime;
        _verifyingAudioSource.volume = verifyingVolume;
        _verifyingAudioSource.Play();
    }

    void StopVerifyingSound()
    {
        if (_verifyingAudioSource == null) return;
        _verifyingSoundStarted = false;
        if (_verifyingAudioSource.isPlaying)
            _verifyingAudioSource.Stop();
    }

    void PlayVerifySuccessSound()
    {
        if (verifySuccessClip == null || _verifyingAudioSource == null) return;
        float clampedTime = Mathf.Clamp(verifySuccessStartTime, 0f, verifySuccessClip.length);
        _verifyingAudioSource.clip = verifySuccessClip;
        _verifyingAudioSource.time = clampedTime;
        _verifyingAudioSource.volume = verifySuccessVolume;
        _verifyingAudioSource.loop = false;
        _verifyingAudioSource.Play();
    }

    void OnTriggerEnter(Collider other)
    {
        if (_alreadyCompleted) return;

        Debug.Log($"[RooftopVerifier.OnTriggerEnter] 物体进入: {other.gameObject.name}");

        if (!IsDroneOrCarried(other))
        {
            Debug.Log($"[RooftopVerifier.OnTriggerEnter] 跳过: {other.gameObject.name} 不是无人机或已抓取物品");
            return;
        }

        if (!_droneInsideRoofZone)
        {
            _droneInsideRoofZone = true;
            Debug.Log($"[RooftopVerifier.OnTriggerEnter] 无人机进入楼顶层区域，光圈已激活={verificationRing != null}");

            if (!_ringShown)
            {
                _ringShown = true;
                if (verificationRing != null)
                {
                    verificationRing.SetActive(true);
                    Debug.Log("[RooftopVerifier.OnTriggerEnter] verificationRing.SetActive(true) 已调用");
                }
            }

            OnRooftopZoneEntered?.Invoke();
            Debug.Log("[RooftopVerifier.OnTriggerEnter] OnRooftopZoneEntered 已触发");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsDroneOrCarried(other))
            _droneInsideRoofZone = false;
    }

    bool IsDroneOrCarried(Collider col)
    {
        if (col.GetComponentInParent<PlaneController>() != null)
            return true;
        var grabbable = col.GetComponentInParent<Grabbable>();
        if (grabbable != null)
        {
            var gripper = grabbable.GetComponentInParent<DroneGripper>();
            if (gripper != null && gripper.IsHolding)
                return true;
        }
        return false;
    }

    /// <summary>重置验证状态（重新开始任务时调用）</summary>
    public void ResetVerifier()
    {
        _alreadyCompleted = false;
        _droneInsideRoofZone = false;
        _droneInsideRing = false;
        _verifyProgress = 0f;
        _ringShown = false;
        _verifyingSoundStarted = false;

        if (_verifyingAudioSource != null && _verifyingAudioSource.isPlaying)
            _verifyingAudioSource.Stop();

        if (verificationRing != null)
            verificationRing.SetActive(false);

        UpdateProgressUI(0f);
    }
}
