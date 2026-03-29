using UnityEngine;

/// <summary>
/// 「linshi」流程：当指定（或场景中全部）缓冲垫都处于显示状态时播放一段音频。
/// </summary>
[AddComponentMenu("MiniGame/Linshi — 全部缓冲垫打开时播放音频")]
public sealed class MiniGameLinshiFlow : MonoBehaviour
{
    [Tooltip("留空则自动收集当前场景中所有 MiniGameBufferPad（含未激活物体上的组件不会计入，仅统计列表内对象）。")]
    [SerializeField] MiniGameBufferPad[] watchedPads;

    [SerializeField] AudioClip completionClip;
    [Tooltip("留空则在本物体上取 AudioSource，再没有则运行时创建一个。")]
    [SerializeField] AudioSource audioSource;

    [Tooltip("勾选后本局只会播放一次（直到重新进入场景）。")]
    [SerializeField] bool playOnlyOncePerScene;

    bool _armedForNextAllOpen = true;
    bool _playedOnceEver;

    void LateUpdate()
    {
        if (completionClip == null)
            return;

        var pads = ResolvePads();
        if (pads.Length == 0)
            return;

        if (playOnlyOncePerScene && _playedOnceEver)
            return;

        int need = 0;
        int open = 0;
        foreach (var p in pads)
        {
            if (p == null)
                continue;
            need++;
            if (p.gameObject.activeInHierarchy)
                open++;
        }

        if (need == 0)
            return;

        bool allOpen = open == need;

        if (allOpen)
        {
            if (_armedForNextAllOpen)
            {
                EnsureAudioSource();
                if (audioSource != null)
                    audioSource.PlayOneShot(completionClip);
                _armedForNextAllOpen = false;
                _playedOnceEver = true;
            }
        }
        else
            _armedForNextAllOpen = true;
    }

    MiniGameBufferPad[] ResolvePads()
    {
        if (watchedPads != null && watchedPads.Length > 0)
            return watchedPads;

        return Object.FindObjectsByType<MiniGameBufferPad>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    void EnsureAudioSource()
    {
        if (audioSource != null)
            return;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    /// <summary>重新允许在「全部打开」时触发（例如重新开始一局）。</summary>
    public void ResetLinshiProgress()
    {
        _armedForNextAllOpen = true;
        _playedOnceEver = false;
    }
}
