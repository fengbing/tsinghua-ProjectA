using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// study 场景叙事控制器：
/// 1. 场景运行 1 秒后播放第一段旁白（黑底字幕 + 配音）
/// 2. 监听 TutorialRing.OnPassed 事件，判断三个阶段（phase 0/1/2）全部通过
/// 3. 每个阶段完成后延迟 0.5 秒播放下一段旁白
/// 4. 第三阶段（phase=2）完成后播放第四段旁白，
///    播完再等待 1 秒 → 黑幕淡入 → 异步预加载 meau → 跳转
/// </summary>
public class StudyNarrativeController : MonoBehaviour
{
    [Header("SystemDialogController（留空则自动查找）")]
    [SerializeField] private SystemDialogController systemDialog;

    [Header("TutorialHud（旁白期间隐藏 hintText）")]
    [SerializeField] private TutorialHud tutorialHud;

    [Header("黑幕 Image（第四段结束后跳转时淡入，留空则自动创建）")]
    [SerializeField] private Image blackScreenImage;

    [Header("旁白 1（场景运行 1 秒后自动播放）")]
    [TextArea(2, 5)]
    [SerializeField] private string narrationText1 = "旁白1文字";
    [SerializeField] private AudioClip narrationAudio1;
    [Range(0f, 1f)]
    [SerializeField] private float narrationVolume1 = 1f;

    [Header("旁白 2（阶段 0 完成后 +0.5s 延迟播放）")]
    [TextArea(2, 5)]
    [SerializeField] private string narrationText2 = "旁白2文字";
    [SerializeField] private AudioClip narrationAudio2;
    [Range(0f, 1f)]
    [SerializeField] private float narrationVolume2 = 1f;

    [Header("旁白 3（阶段 1 完成后 +0.5s 延迟播放）")]
    [TextArea(2, 5)]
    [SerializeField] private string narrationText3 = "旁白3文字";
    [SerializeField] private AudioClip narrationAudio3;
    [Range(0f, 1f)]
    [SerializeField] private float narrationVolume3 = 1f;

    [Header("旁白 4（阶段 2 完成后 +0.5s 延迟播放，播完 1s 后跳转 meau）")]
    [TextArea(2, 5)]
    [SerializeField] private string narrationText4 = "旁白4文字";
    [SerializeField] private AudioClip narrationAudio4;
    [Range(0f, 1f)]
    [SerializeField] private float narrationVolume4 = 1f;

    [Header("第一人称提示（Phase 2 完成后、播放旁白4前显示）")]
    [TextArea(2, 5)]
    [SerializeField] private string firstPersonHintText = "请按 Alt 打开第一人称";

    [Header("时序参数")]
    [Tooltip("阶段完成后延迟播放下一段旁白的秒数（阶段 0/1/2 均适用）")]
    [SerializeField] private float phaseDelaySeconds = 0.5f;
    [Tooltip("第四段旁白播完后，等待跳转的秒数")]
    [SerializeField] private float transitionDelaySeconds = 0f;
    [Tooltip("黑幕淡入持续时间（秒）")]
    [SerializeField] private float blackScreenFadeDuration = 0.3f;
    [Tooltip("跳转目标场景名")]
    [SerializeField] private string targetSceneName = "menu 1";

    /// <summary>阶段 0 → 旁白2，阶段 1 → 旁白3，阶段 2 → 旁白4</summary>
    private static readonly string[] NarrationTexts   = { null, null, null, null };
    private static readonly AudioClip[] NarrationClips = { null, null, null, null };
    private static readonly float[] NarrationVolumes  = { 1f, 1f, 1f, 1f };

    private readonly HashSet<int> _passedRings = new();

    /// <summary>
    /// 当前监听的阶段：0=等待阶段0全部通过后播旁白2，
    /// 1=等待阶段1全部通过后播旁白3，
    /// 2=等待阶段2全部通过后播旁白4（之后不再监听）
    /// </summary>
    private int _currentListeningPhase;

    /// <summary>是否已完成所有旁白播放（防止重复触发）</summary>
    private bool _allDone;

    /// <summary>标记是否已订阅光圈事件（避免重复订阅）</summary>
    private bool _subscribed;

    /// <summary>阶段 0 是否已在播放旁白 2（防止重复触发）</summary>
    private bool _phase0Triggered;
    private bool _phase1Triggered;
    private bool _phase2Triggered;

    /// <summary>Phase 2 完成后，等待玩家打开第一人称的标记</summary>
    private bool _waitingForFirstPerson;

    private Canvas _canvas;

    private void Awake()
    {
        if (systemDialog == null)
            systemDialog = FindObjectOfType<SystemDialogController>();
        if (tutorialHud == null)
            tutorialHud = FindObjectOfType<TutorialHud>();

        NarrationTexts[0] = narrationText1;
        NarrationTexts[1] = narrationText2;
        NarrationTexts[2] = narrationText3;
        NarrationTexts[3] = narrationText4;

        NarrationClips[0] = narrationAudio1;
        NarrationClips[1] = narrationAudio2;
        NarrationClips[2] = narrationAudio3;
        NarrationClips[3] = narrationAudio4;

        NarrationVolumes[0] = narrationVolume1;
        NarrationVolumes[1] = narrationVolume2;
        NarrationVolumes[2] = narrationVolume3;
        NarrationVolumes[3] = narrationVolume4;

        Debug.Log($"[StudyNarrative] Awake，narrationText3=[{narrationText3}]，narrationAudio3={narrationAudio3?.name ?? "(空)"}，narrationText4=[{narrationText4}]，narrationAudio4={narrationAudio4?.name ?? "(空)"}，targetSceneName={targetSceneName}");
    }

    private void Start()
    {
        _canvas = FindObjectOfType<Canvas>();
        EnsureBlackScreenImage();

        // 立即订阅所有 TutorialRing 的 OnPassed 事件
        SubscribeToAllRings();

        // 协变 1：场景运行 1 秒后播放旁白 1
        StartCoroutine(DelayedStartNarration());
    }

    private void OnDestroy()
    {
        UnsubscribeFromAllRings();
    }

    private IEnumerator DelayedStartNarration()
    {
        yield return new WaitForSecondsRealtime(1f);
        if (_allDone) yield break;
        yield return PlayNarrationLine(0);
        // 旁白1播完后，开始监听阶段0的完成
        // 此时 _currentListeningPhase 保持为 0，直到阶段0完成才递增
    }

    private void SubscribeToAllRings()
    {
        if (_subscribed) return;
        _subscribed = true;

        // 必须用 FindObjectsByType + FindObjectsInactive.Include，找到所有 ring（包括当前 phase 未激活的）
        var allRings = FindObjectsByType<TutorialRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[ALL] StudyNarrative.SubscribeToAllRings 已订阅 {allRings.Length} 个 TutorialRing，ringIndex列表=[{string.Join(",", allRings.Select(r => r.ringIndex))}]，phase列表=[{string.Join(",", allRings.Select(r => r.phase))}]");

        foreach (var ring in allRings)
        {
            ring.OnPassed += HandleRingPassed;
        }
    }

    private void UnsubscribeFromAllRings()
    {
        foreach (var ring in FindObjectsByType<TutorialRing>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            ring.OnPassed -= HandleRingPassed;
        }
    }

    private void HandleRingPassed(int ringIndex)
    {
        Debug.Log($"[ALL] StudyNarrative.HandleRingPassed 被调用，ring={ringIndex}");

        // 每次调用都确保自己是订阅者（防御性：先取消再订阅，避免遗漏）
        foreach (var ring in FindObjectsByType<TutorialRing>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            ring.OnPassed -= HandleRingPassed;
            ring.OnPassed += HandleRingPassed;
        }
        _subscribed = true;

        if (_passedRings.Contains(ringIndex)) return;

        _passedRings.Add(ringIndex);

        var allRings = FindObjectsByType<TutorialRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var thisRing = allRings.FirstOrDefault(r => r.ringIndex == ringIndex);
        if (thisRing == null)
        {
            Debug.LogWarning($"[StudyNarrative] HandleRingPassed({ringIndex}) 未找到对应 TutorialRing");
            return;
        }

        int ringPhase = thisRing.phase;
        Debug.Log($"[StudyNarrative] HandleRingPassed({ringIndex})，phase={ringPhase}，" +
                  $"_phase0Triggered={_phase0Triggered} _phase1Triggered={_phase1Triggered} _phase2Triggered={_phase2Triggered}");

        if (ringPhase == 0 && !_phase0Triggered)
            TryTriggerPhaseComplete(0, allRings);
        else if (ringPhase == 1 && !_phase1Triggered)
            TryTriggerPhaseComplete(1, allRings);
        else if (ringPhase == 2 && !_phase2Triggered)
            TryTriggerPhaseComplete(2, allRings);
    }

    private void TryTriggerPhaseComplete(int phase, TutorialRing[] allRings)
    {
        // 统计本阶段总共有多少个光圈
        var ringsInPhase = allRings.Where(r => r.phase == phase).ToList();
        Debug.Log($"[StudyNarrative] TryTriggerPhaseComplete({phase})，本阶段光圈数={ringsInPhase.Count}，" +
                  $"ringIndices=[{string.Join(",", ringsInPhase.Select(r => r.ringIndex))}]，" +
                  $"已通过=[{string.Join(",", _passedRings)}]");

        if (ringsInPhase.Count == 0) return;

        // 统计本阶段有多少已通过（只统计已标记过的）
        int passedInPhase = ringsInPhase.Count(r => _passedRings.Contains(r.ringIndex));
        Debug.Log($"[StudyNarrative] TryTriggerPhaseComplete({phase})，已通过={passedInPhase}/{ringsInPhase.Count}");

        // 只有全部通过才触发
        if (passedInPhase < ringsInPhase.Count) return;

        Debug.Log($"[StudyNarrative] TryTriggerPhaseComplete({phase}) 全部通过，即将播放旁白！");

        // 标记该阶段已触发
        if (phase == 0) _phase0Triggered = true;
        else if (phase == 1) _phase1Triggered = true;
        else if (phase == 2) _phase2Triggered = true;

        // 旁白索引：phase 0 → 旁白2(index 1)，phase 1 → 旁白3(index 2)，phase 2 → 旁白4(index 3)
        int narrationIndex = phase + 1;
        float delay = phaseDelaySeconds; // 均为 0.5s

        // Phase 2 需要玩家先打开第一人称，再播放旁白4
        if (phase == 2)
        {
            _waitingForFirstPerson = true;
            Debug.Log("[StudyNarrative] Phase 2 全部通过，等待玩家打开第一人称...");
            tutorialHud?.SetWaitingForFirstPerson(true);
            StartCoroutine(WaitForFirstPersonThenNarration());
            return;
        }

        StartCoroutine(DelayedNarrationAfterPhase(narrationIndex, delay, phase));
    }

    private IEnumerator DelayedNarrationAfterPhase(int narrationIndex, float delay, int phase)
    {
        Debug.Log($"[StudyNarrative] DelayedNarrationAfterPhase 开始，narrationIndex={narrationIndex}，delay={delay}，phase={phase}");
        yield return new WaitForSecondsRealtime(delay);
        if (_allDone) yield break;

        Debug.Log($"[StudyNarrative] DelayedNarrationAfterPhase 等待结束，开始 PlayNarrationLine({narrationIndex})，text={(NarrationTexts[narrationIndex] ?? "(空)")}，clip={NarrationClips[narrationIndex]?.name ?? "(空)"}");
        yield return PlayNarrationLine(narrationIndex);
        Debug.Log($"[StudyNarrative] DelayedNarrationAfterPhase PlayNarrationLine({narrationIndex}) 返回，phase={phase}，_currentListeningPhase={_currentListeningPhase}，_phase0Triggered={_phase0Triggered} _phase1Triggered={_phase1Triggered} _phase2Triggered={_phase2Triggered}");

        // phase 2（第三阶段）完成后，递增监听阶段并等待第四段播完再跳转
        if (phase == 2)
        {
            _currentListeningPhase++;
            StartCoroutine(FinishWithTransition());
        }
        else
        {
            // 旁白2/3播完后，继续监听下一阶段
            _currentListeningPhase++;
        }
    }

    /// <summary>
    /// 播放单段旁白：
    /// - 设置配音音量并播放
    /// - 通过 systemDialog.ShowSubtitle 显示黑底字幕
    /// - 等待配音时长后结束（不手动调用 HideSubtitle，
    ///   让 ShowSubtitleRoutine 自行完成淡出和清理）
    /// </summary>
    private IEnumerator PlayNarrationLine(int index)
    {
        string text   = NarrationTexts[index];
        AudioClip clip = NarrationClips[index];
        float volume  = NarrationVolumes[index];

        Debug.Log($"[StudyNarrative] PlayNarrationLine({index})，text={(text ?? "(空)")}，clip={(clip != null ? clip.name + " (loaded)" : "(空/未加载)")}，volume={volume}");

        if (systemDialog == null)
        {
            Debug.LogWarning("[StudyNarrativeController] systemDialog 为空，跳过旁白");
            yield break;
        }

        // 通知 HUD 旁白开始：隐藏 hintText，避免遮挡旁白字幕
        if (tutorialHud != null)
            tutorialHud.SetNarrationActive(true);

        if (systemDialog.VoiceSource != null)
            systemDialog.VoiceSource.volume = volume;

        float duration;
        if (clip != null && !string.IsNullOrEmpty(text))
        {
            systemDialog.ShowSubtitle(text, clip.length, clip);
            duration = clip.length;
        }
        else if (clip != null)
        {
            if (systemDialog.VoiceSource != null)
                systemDialog.VoiceSource.PlayOneShot(clip);
            duration = clip.length;
        }
        else if (!string.IsNullOrEmpty(text))
        {
            float estimatedDuration = text.Length * 0.04f + 0.5f;
            systemDialog.ShowSubtitle(text, estimatedDuration, null);
            duration = estimatedDuration;
        }
        else
        {
            duration = 1.5f;
        }

        // 超时保护：配音最长等 duration * 2 秒（配音卡住时防止永久挂起）
        float elapsed = 0f;
        while (elapsed < duration * 2f)
        {
            yield return new WaitForSecondsRealtime(0.1f);
            elapsed += 0.1f;
        }
        Debug.Log($"[StudyNarrative] PlayNarrationLine({index}) 结束，elapsed={elapsed:F2}s");

        // 通知 HUD 旁白结束：恢复 hintText 显示
        if (tutorialHud != null)
            tutorialHud.SetNarrationActive(false);
    }

    /// <summary>
    /// Phase 2 完成后：等待玩家打开第一人称，再播放旁白4。
    /// 每帧检测玩家是否按下了 Alt 键，或者 FollowCamera 已切换到第一人称模式。
    /// </summary>
    private IEnumerator WaitForFirstPersonThenNarration()
    {
        Debug.Log("[StudyNarrative] WaitForFirstPersonThenNarration 开始，等待第一人称模式...");

        while (_waitingForFirstPerson)
        {
            // 玩家按下了 Alt 键，或者 FollowCamera 已切换到第一人称模式
            bool altPressed = Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt);
            bool inFirstPerson = IsFirstPersonMode();

            if (altPressed || inFirstPerson)
            {
                Debug.Log($"[StudyNarrative] 检测到第一人称切换，altPressed={altPressed}，inFirstPerson={inFirstPerson}");
                _waitingForFirstPerson = false;
                tutorialHud?.SetWaitingForFirstPerson(false);
                tutorialHud?.ShowCompletedFirstPerson();

                // 延迟 0.5 秒后播放旁白4
                yield return new WaitForSecondsRealtime(phaseDelaySeconds);
                if (_allDone) yield break;

                yield return PlayNarrationLine(3);
                if (_allDone) yield break;

                _currentListeningPhase++;
                StartCoroutine(FinishWithTransition());
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// 通过反射读取 FollowCamera 的 _firstPersonMode 私有字段，
    /// 与 GameUi.IsFirstPersonMode() 保持一致。
    /// </summary>
    private bool IsFirstPersonMode()
    {
        var cam = FindObjectOfType<FollowCamera>();
        if (cam == null) return false;
        var field = typeof(FollowCamera).GetField("_firstPersonMode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field != null && (bool)field.GetValue(cam);
    }

    private IEnumerator FinishWithTransition()
    {
        Debug.Log($"[StudyNarrative] FinishWithTransition，targetSceneName={targetSceneName}");
        _allDone = true;

        // 优先使用 SceneTransitionHelper（与 BackupDialogEvents 完全一致的过渡效果）
        var helper = FindObjectOfType<SceneTransitionHelper>();
        if (helper != null)
        {
            helper.StartTransition(targetSceneName, transitionDelaySeconds, blackScreenFadeDuration);
            yield break;
        }

        // 兜底：自己实现黑幕淡入 + 跳转
        yield return new WaitForSecondsRealtime(transitionDelaySeconds);

        if (blackScreenImage != null)
        {
            blackScreenImage.gameObject.SetActive(true);
            yield return StartCoroutine(FadeInImage(blackScreenImage, blackScreenFadeDuration));
        }

        if (!string.IsNullOrEmpty(targetSceneName))
        {
            AsyncOperation preloadOp = SceneManager.LoadSceneAsync(targetSceneName);
            if (preloadOp != null)
            {
                preloadOp.allowSceneActivation = false;
                yield return new WaitUntil(() => preloadOp.progress >= 0.9f);
                preloadOp.allowSceneActivation = true;
            }
            else
            {
                SceneManager.LoadScene(targetSceneName);
            }
        }
    }

    private IEnumerator FadeInImage(Image image, float duration)
    {
        float elapsed = 0f;
        var color = image.color;
        color.a = 0f;
        image.color = color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            color.a = t;
            image.color = color;
            yield return null;
        }
        color.a = 1f;
        image.color = color;
    }

    private void EnsureBlackScreenImage()
    {
        if (blackScreenImage != null) return;

        if (_canvas == null) _canvas = FindObjectOfType<Canvas>();
        if (_canvas == null)
        {
            Debug.LogWarning("[StudyNarrativeController] 场景中未找到 Canvas，无法自动创建黑幕 Image");
            return;
        }

        var go = new GameObject("StudyBlackScreen", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_canvas.transform, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        blackScreenImage = go.GetComponent<Image>();
        blackScreenImage.color = Color.black;
        blackScreenImage.raycastTarget = false;

        // 初始隐藏，第四段结束后才显示
        go.SetActive(false);
    }
}
