using UnityEngine;

/// <summary>
/// 背景音乐管理器：使用 AudioSource 的 loop 属性实现无缝循环播放
/// </summary>
public class BgMusicManager : MonoBehaviour
{
    [Tooltip("背景音乐音频片段")]
    [SerializeField] private AudioClip musicClip;

    [Tooltip("音量")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.5f;

    [Tooltip("是否淡入")]
    [SerializeField] private bool fadeIn = true;

    [Tooltip("淡入时长（秒）")]
    [SerializeField] private float fadeInDuration = 1f;

    private AudioSource _audioSource;
    private float _targetVolume;

    private void Awake()
    {
        // 创建或获取 AudioSource 组件
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 配置 AudioSource
        _audioSource.playOnAwake = false;
        _audioSource.loop = true; // 关键：设置循环
        _audioSource.clip = musicClip;
        _audioSource.volume = fadeIn ? 0f : volume;

        _targetVolume = volume;
    }

    private void Start()
    {
        if (musicClip != null)
        {
            _audioSource.Play();

            if (fadeIn)
            {
                StartCoroutine(FadeInCoroutine());
            }
        }
    }

    private System.Collections.IEnumerator FadeInCoroutine()
    {
        float elapsed = 0f;
        float startVolume = _audioSource.volume;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            _audioSource.volume = Mathf.Lerp(startVolume, _targetVolume, elapsed / fadeInDuration);
            yield return null;
        }

        _audioSource.volume = _targetVolume;
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    public void Stop()
    {
        _audioSource.Stop();
    }

    /// <summary>
    /// 暂停播放
    /// </summary>
    public void Pause()
    {
        _audioSource.Pause();
    }

    /// <summary>
    /// 恢复播放
    /// </summary>
    public void Resume()
    {
        _audioSource.UnPause();
    }

    /// <summary>
    /// 设置音量
    /// </summary>
    public void SetVolume(float newVolume)
    {
        _targetVolume = Mathf.Clamp01(newVolume);
        _audioSource.volume = _targetVolume;
    }
}
