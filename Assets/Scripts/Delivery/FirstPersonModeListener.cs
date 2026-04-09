using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 第一人称模式监听器。
/// 通过反射读取 FollowCamera._firstPersonMode，当模式切换时通知 DeliveryPhaseManager。
/// 挂在与 FollowCamera 同一 GameObject 上。
/// </summary>
public class FirstPersonModeListener : MonoBehaviour
{
    [SerializeField] FollowCamera followCamera;

    [Header("音效 - 第一人称扫描")]
    [Tooltip("开启第一人称视角时播放的音效（如扫描开启音效）")]
    [SerializeField] AudioClip firstPersonScanClip;
    [Tooltip("第一人称扫描音效的音量（0~1）")]
    [Range(0f, 1f)][SerializeField] float firstPersonScanVolume = 1f;
    [Tooltip("第一人称扫描音效从第几秒开始播放")]
    [SerializeField] float firstPersonScanStartTime = 0f;

    FieldInfo _firstPersonField;
    bool _wasFirstPerson;
    AudioSource _audioSource;

    public event Action OnFirstPersonEnabled;
    public event Action OnFirstPersonDisabled;

    void Awake()
    {
        if (followCamera == null)
            followCamera = GetComponent<FollowCamera>();

        if (followCamera != null)
            _firstPersonField = typeof(FollowCamera).GetField("_firstPersonMode",
                BindingFlags.NonPublic | BindingFlags.Instance);

        if (_firstPersonField == null)
            Debug.LogWarning("[FirstPersonModeListener] 未找到 _firstPersonMode 字段！");

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }

    void Update()
    {
        if (_firstPersonField == null || followCamera == null) return;

        bool nowFirstPerson = (bool)_firstPersonField.GetValue(followCamera);

        if (nowFirstPerson != _wasFirstPerson)
        {
            _wasFirstPerson = nowFirstPerson;
            Debug.Log($"[FirstPersonModeListener] 第一人称模式切换: {nowFirstPerson}");

            if (nowFirstPerson)
            {
                PlayFirstPersonScanSound();
                OnFirstPersonEnabled?.Invoke();
            }
            else
            {
                OnFirstPersonDisabled?.Invoke();
            }
        }
    }

    void PlayFirstPersonScanSound()
    {
        if (firstPersonScanClip == null || _audioSource == null) return;
        float clampedTime = Mathf.Clamp(firstPersonScanStartTime, 0f, firstPersonScanClip.length);
        _audioSource.clip = firstPersonScanClip;
        _audioSource.time = clampedTime;
        _audioSource.volume = firstPersonScanVolume;
        _audioSource.loop = false;
        _audioSource.Play();
    }
}
