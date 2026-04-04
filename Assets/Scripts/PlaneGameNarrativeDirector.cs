using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PlaneGame 叙事音频：进场 BGM；HUD 同源距离 ≤ 阈值时停 BGM 并播放一次性近距音效；
/// 成功后顺序播放三段语音并加载 Level 2。
/// </summary>
public class PlaneGameNarrativeDirector : MonoBehaviour
{
    [Header("距离（与 DistanceHudStrip 上 DistanceToTargetSource 一致）")]
    [SerializeField] DistanceToTargetSource distanceSource;
    [SerializeField] float proximityThresholdMeters = 50f;
    [Tooltip("若为 true：离开阈值后再进入会再次播放近距音效（默认 false = 每局仅一次）。")]
    [SerializeField] bool resetProximityCueWhenLeavingThreshold;

    [Header("音频")]
    [Tooltip("留空则在同物体上添加并用于循环 BGM")]
    [SerializeField] AudioSource bgmSource;
    [Tooltip("留空则自动创建子物体用于近距音效与语音")]
    [SerializeField] AudioSource voiceSource;
    [SerializeField] AudioClip bgmLoop;
    [SerializeField] AudioClip proximityStinger;
    [SerializeField] AudioClip voiceLine1;
    [SerializeField] AudioClip voiceLine2;
    [SerializeField] AudioClip voiceLine3;

    [Header("过场")]
    [SerializeField] string nextSceneName = "Level 2";

    bool _proximityCuePlayed;
    bool _wasInsideProximity;
    bool _outroStarted;
    bool _loggedMissingDistance;

    public bool IsOutroStarted => _outroStarted;

    void Awake()
    {
        if (bgmSource == null)
        {
            bgmSource = GetComponent<AudioSource>();
            if (bgmSource == null)
                bgmSource = gameObject.AddComponent<AudioSource>();
        }
        bgmSource.playOnAwake = false;
        bgmSource.loop = false;

        if (voiceSource == null)
        {
            var voiceGo = new GameObject("NarrativeVoiceAudio");
            voiceGo.transform.SetParent(transform, false);
            voiceSource = voiceGo.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.loop = false;
        }

        if (distanceSource == null)
            distanceSource = FindObjectOfType<DistanceToTargetSource>();
    }

    void Start()
    {
        if (bgmLoop == null || bgmSource == null)
            return;
        bgmSource.clip = bgmLoop;
        bgmSource.loop = true;
        bgmSource.Play();
    }

    void Update()
    {
        if (distanceSource == null)
        {
            if (!_loggedMissingDistance)
            {
                _loggedMissingDistance = true;
                Debug.LogWarning("[PlaneGameNarrativeDirector] 未指定 DistanceToTargetSource，近距音效已禁用。");
            }
            return;
        }

        float d = distanceSource.GetDistanceMeters();
        bool inside = d <= proximityThresholdMeters;

        if (resetProximityCueWhenLeavingThreshold)
        {
            if (_wasInsideProximity && !inside)
                _proximityCuePlayed = false;
        }
        _wasInsideProximity = inside;

        if (!inside || _proximityCuePlayed)
            return;

        _proximityCuePlayed = true;
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
        if (proximityStinger != null && voiceSource != null)
            voiceSource.PlayOneShot(proximityStinger);
    }

    /// <summary>由放置检测（触发区等）在投递成功时调用。</summary>
    public void NotifyDeliveryComplete()
    {
        if (_outroStarted)
            return;
        _outroStarted = true;
        StartCoroutine(PlayOutroAndLoad());
    }

    IEnumerator PlayOutroAndLoad()
    {
        if (voiceSource != null)
        {
            yield return PlayClipBlocking(voiceLine1);
            yield return PlayClipBlocking(voiceLine2);
            yield return PlayClipBlocking(voiceLine3);
        }
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
    }

    IEnumerator PlayClipBlocking(AudioClip clip)
    {
        if (clip == null || voiceSource == null)
            yield break;
        voiceSource.PlayOneShot(clip);
        yield return new WaitForSeconds(clip.length);
    }
}
