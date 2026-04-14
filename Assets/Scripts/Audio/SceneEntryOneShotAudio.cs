using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景加载后播放一次非循环音频。可限制为「整局游戏（本次运行）只播一次」或「写入存档、以后进场景也不再播」。
/// </summary>
public class SceneEntryOneShotAudio : MonoBehaviour
{
    const string PrefsPrefix = "SceneEntryOneShotAudio.";

    static readonly HashSet<string> s_playedThisRun = new();

    [SerializeField] AudioClip clip;
    [Tooltip("留空则在本物体上获取或创建 AudioSource")]
    [SerializeField] AudioSource audioSource;
    [Tooltip("进入场景后延迟（秒，不受 Time.timeScale 影响）")]
    [SerializeField] float delaySeconds;
    [Tooltip("勾选：本次游戏运行期间，同一去重键只播一次（再进该场景不会重播）")]
    [SerializeField] bool oncePerGameRun = true;
    [Tooltip("勾选：写入 PlayerPrefs，关掉游戏再开也不会再播（需配合「整局一次」逻辑）")]
    [SerializeField] bool persistAcrossAppLaunches;
    [Tooltip("自定义去重键；多个物体可共键以共用「只播一次」。留空则用「场景名 + 剪辑名」")]
    [SerializeField] string customDedupeKey;
    [Header("系统对话（可选）")]
    [SerializeField] bool autoAttachSceneEntryDialog = true;
    [TextArea(2, 5)]
    [SerializeField] string dialogText = "系统提示：欢迎进入场景。";
    [SerializeField] AudioClip dialogVoiceClip;
    [SerializeField] float dialogDelaySeconds;
    [SerializeField] TMP_FontAsset dialogFont;
    [SerializeField] int dialogFontSize;
    [SerializeField] bool dialogAutoFitTextToVoiceEnd = true;
    [SerializeField] float dialogExtraSecondsAfterVoice = 1f;

    [Header("路线规划小游戏")]
    [Tooltip("开场 OneShot 播完后自动打开（见 RoutePlanningMiniGameController.OpenFromSceneEntry）")]
    [SerializeField] bool openRoutePlanningAfterStartClip;
    [SerializeField] RoutePlanningMiniGameController routePlanningAfterStartClip;
    [Tooltip("在剪辑时长之外再多等几秒再开规划")]
    [SerializeField] float extraSecondsAfterClipBeforeOpenPlanner;
    [Tooltip("再等系统黑底对话播完再开规划，避免挡字幕")]
    [SerializeField] bool waitForSystemDialogIdleBeforeOpenPlanner = true;

    void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        if (autoAttachSceneEntryDialog)
        {
            var trigger = GetComponent<SceneEntryDialogTrigger>();
            if (trigger == null)
                trigger = gameObject.AddComponent<SceneEntryDialogTrigger>();
            trigger.ConfigureDefaults(
                dialogText,
                dialogVoiceClip,
                dialogDelaySeconds,
                dialogFont,
                dialogFontSize,
                dialogAutoFitTextToVoiceEnd,
                dialogExtraSecondsAfterVoice);
        }
    }

    void Start()
    {
        if (HasAlreadyPlayedForSettings())
            return;

        bool wantPlanner = openRoutePlanningAfterStartClip && routePlanningAfterStartClip != null;
        if (clip == null && !wantPlanner)
            return;

        StartCoroutine(EntryAudioAndOptionalPlannerRoutine());
    }

    string BuildDedupeKey()
    {
        if (!string.IsNullOrWhiteSpace(customDedupeKey))
            return customDedupeKey.Trim();
        string clipPart = clip != null ? clip.name : "NoOneShotClip";
        return $"{SceneManager.GetActiveScene().name}\u001f{clipPart}";
    }

    bool HasAlreadyPlayedForSettings()
    {
        string key = BuildDedupeKey();
        if (persistAcrossAppLaunches)
            return PlayerPrefs.GetInt(PrefsPrefix + key, 0) != 0;
        if (oncePerGameRun)
            return s_playedThisRun.Contains(key);
        return false;
    }

    void MarkDone()
    {
        string key = BuildDedupeKey();
        if (persistAcrossAppLaunches)
        {
            PlayerPrefs.SetInt(PrefsPrefix + key, 1);
            PlayerPrefs.Save();
        }

        if (oncePerGameRun)
            s_playedThisRun.Add(key);
    }

    void PlayAndMarkDone()
    {
        audioSource.PlayOneShot(clip);
        MarkDone();
    }

    IEnumerator EntryAudioAndOptionalPlannerRoutine()
    {
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);

        if (clip != null && audioSource != null)
        {
            PlayAndMarkDone();
            yield return new WaitForSecondsRealtime(clip.length + Mathf.Max(0f, extraSecondsAfterClipBeforeOpenPlanner));
        }
        else
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, extraSecondsAfterClipBeforeOpenPlanner));

        if (!openRoutePlanningAfterStartClip || routePlanningAfterStartClip == null)
            yield break;

        if (waitForSystemDialogIdleBeforeOpenPlanner)
        {
            var dlg = FindObjectOfType<SystemDialogController>();
            if (dlg != null)
                yield return dlg.WaitUntilDialogIdle();
        }

        routePlanningAfterStartClip.OpenFromSceneEntry();
        if (clip == null)
            MarkDone();
    }
}
