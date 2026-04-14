using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class GameUi : MonoBehaviour
{
    [SerializeField] private GameObject windowUi;
    [SerializeField] private GameObject frontSightUi;
    [SerializeField] private float timeInterval = 60f;
    [SerializeField] private GameObject power;
    [SerializeField] private GameObject power1;
    [SerializeField] private GameObject power2;
    [SerializeField] private GameObject power3;
    [SerializeField] private GameObject power4;
    [SerializeField] private GameObject signal;
    [SerializeField] private GameObject signal1;
    [SerializeField] private GameObject signal2;
    [SerializeField] private GameObject signal3;
    [SerializeField] private GameObject signal4;
    [SerializeField] private Transform plane;
    [SerializeField] private Transform destination;
    [SerializeField] private float distanceThreshold1 = 50f;
    [SerializeField] private float distanceThreshold2 = 100f;
    [SerializeField] private float distanceThreshold3 = 150f;
    [SerializeField] private float distanceThreshold4 = 200f;
    [SerializeField] private Image radialFillImage;
    [SerializeField] private float showDuration = 0.5f;

    [Header("信号增强音效")]
    [Tooltip("无人机信号增强时播放的音效")]
    [SerializeField] private AudioClip signalUpClip;
    [Tooltip("信号增强音效的音量（0~1）")]
    [Range(0f, 1f)][SerializeField] private float signalUpVolume = 0.6f;
    [Tooltip("信号增强音效从第几秒开始播放")]
    [SerializeField] private float signalUpStartTime = 0f;

    [Header("电量耗尽音效")]
    [Tooltip("无人机电量耗尽时播放的音效")]
    [SerializeField] private AudioClip powerDepletedClip;
    [Tooltip("电量耗尽音效的音量（0~1）")]
    [Range(0f, 1f)][SerializeField] private float powerDepletedVolume = 1f;
    [Tooltip("电量耗尽音效从第几秒开始播放")]
    [SerializeField] private float powerDepletedStartTime = 0f;

    private bool isUiVisible = false;
    private int currentPowerLevel = 4;
    private float timer = 0f;
    private bool countdownStarted = false;
    private GameObject[] powerObjects;
    private GameObject[] signalObjects;
    private bool isFirstPerson = false;
    private Coroutine showCoroutine;
    private AudioSource _audioSource;
    private int _lastSignalLevel = -1;
    private bool _powerDepletedPlayed = false;

    void Start()
    {
        isFirstPerson = false;

        if (windowUi != null)
        {
            windowUi.SetActive(false);
        }
        if (frontSightUi != null)
        {
            frontSightUi.SetActive(false);
        }
        if (radialFillImage != null)
        {
            radialFillImage.fillAmount = 0f;
            radialFillImage.gameObject.SetActive(false);
        }

        powerObjects = new GameObject[] { power, power1, power2, power3, power4 };
        signalObjects = new GameObject[] { signal, signal1, signal2, signal3, signal4 };
        InitializePowerDisplay();
        InitializeSignalDisplay();
        countdownStarted = true;

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _lastSignalLevel = -1;
    }

    void Update()
    {
        bool currentFirstPerson = IsFirstPersonMode();

        if (Input.GetKeyDown(KeyCode.Escape))
            ToggleUi();

        if (currentFirstPerson != isFirstPerson)
        {
            isFirstPerson = currentFirstPerson;
            if (isFirstPerson)
            {
                if (frontSightUi != null)
                    frontSightUi.SetActive(true);
                if (showCoroutine != null)
                    StopCoroutine(showCoroutine);
                showCoroutine = StartCoroutine(ShowUiFrame());
            }
            else
            {
                if (frontSightUi != null)
                    frontSightUi.SetActive(false);
                if (radialFillImage != null)
                {
                    radialFillImage.fillAmount = 0f;
                    radialFillImage.gameObject.SetActive(false);
                }
            }
        }

        if (countdownStarted && !isUiVisible)
        {
            timer += Time.deltaTime;
            if (timer >= timeInterval)
            {
                timer = 0f;
                DecreasePower();
            }
        }

        UpdateSignalDisplay();
    }

    private bool IsFirstPersonMode()
    {
        FollowCamera cam = GetComponent<FollowCamera>();
        if (cam == null)
            cam = FindObjectOfType<FollowCamera>();

        if (cam != null)
        {
            System.Reflection.FieldInfo field = typeof(FollowCamera).GetField("_firstPersonMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
                return (bool)field.GetValue(cam);
        }
        return false;
    }

    private IEnumerator ShowUiFrame()
    {
        if (radialFillImage != null)
        {
            radialFillImage.fillAmount = 0f;
            radialFillImage.gameObject.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < showDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / showDuration;

            if (radialFillImage != null)
                radialFillImage.fillAmount = progress;

            yield return null;
        }

        if (radialFillImage != null)
            radialFillImage.fillAmount = 1f;
    }

    private void InitializeSignalDisplay()
    {
        for (int i = 0; i < signalObjects.Length; i++)
        {
            if (signalObjects[i] != null)
            {
                signalObjects[i].SetActive(false);
            }
        }
    }

    private void UpdateSignalDisplay()
    {
        if (plane == null || destination == null)
            return;

        float distance = Vector3.Distance(plane.position, destination.position);
        int signalLevel = GetSignalLevel(distance);

        if (signalLevel > _lastSignalLevel && _lastSignalLevel != -1)
        {
            PlaySignalUpEffect();
        }
        _lastSignalLevel = signalLevel;

        for (int i = 0; i < signalObjects.Length; i++)
        {
            if (signalObjects[i] != null)
            {
                signalObjects[i].SetActive(i == signalLevel);
            }
        }
    }

    private int GetSignalLevel(float distance)
    {
        if (distance > distanceThreshold4)
            return 0;
        else if (distance > distanceThreshold3)
            return 1;
        else if (distance > distanceThreshold2)
            return 2;
        else if (distance > distanceThreshold1)
            return 3;
        else
            return 4;
    }

    private void InitializePowerDisplay()
    {
        for (int i = 0; i < powerObjects.Length; i++)
        {
            if (powerObjects[i] != null)
            {
                powerObjects[i].SetActive(i == 4);
            }
        }
        currentPowerLevel = 4;
    }

    private void ToggleUi()
    {
        if (RoutePlanningMiniGameController.IsPlanningUiOpen)
            return;
        if (FacadeRescueSessionState.IsOpen)
            return;

        isUiVisible = !isUiVisible;

        if (windowUi != null)
        {
            windowUi.SetActive(isUiVisible);
        }

        if (isFirstPerson)
        {
            if (frontSightUi != null)
            {
                frontSightUi.SetActive(!isUiVisible);
            }
        }

        if (isUiVisible)
            GlobalGamePause.Pause();
        else
            GlobalGamePause.Resume();
    }

    private void DecreasePower()
    {
        if (currentPowerLevel > 0)
        {
            if (powerObjects[currentPowerLevel] != null)
            {
                powerObjects[currentPowerLevel].SetActive(false);
            }

            currentPowerLevel--;

            if (powerObjects[currentPowerLevel] != null)
            {
                powerObjects[currentPowerLevel].SetActive(true);
            }

            if (currentPowerLevel == 0)
            {
                PlayPowerDepletedEffect();
                EndGame();
            }
        }
    }

    private void EndGame()
    {
        countdownStarted = false;
        Debug.Log("游戏结束！");
        StartCoroutine(ShowPowerDepletedDialogAfterDelay());
    }

    private IEnumerator ShowPowerDepletedDialogAfterDelay()
    {
        yield return new WaitForSecondsRealtime(1f);
        if (BackupDialogEvents.Instance != null)
            BackupDialogEvents.Instance.ShowPowerDepletedDialog();
        else
            Debug.LogWarning("[GameUi] BackupDialogEvents.Instance 为空，无法显示电量耗尽弹窗");
    }

    private void PlaySignalUpEffect()
    {
        if (signalUpClip == null || _audioSource == null) return;
        float clampedTime = Mathf.Clamp(signalUpStartTime, 0f, signalUpClip.length);
        _audioSource.clip = signalUpClip;
        _audioSource.time = clampedTime;
        _audioSource.volume = signalUpVolume;
        _audioSource.loop = false;
        _audioSource.Play();
    }

    private void PlayPowerDepletedEffect()
    {
        if (powerDepletedClip == null || _audioSource == null) return;
        if (_powerDepletedPlayed) return;
        _powerDepletedPlayed = true;
        float clampedTime = Mathf.Clamp(powerDepletedStartTime, 0f, powerDepletedClip.length);
        _audioSource.clip = powerDepletedClip;
        _audioSource.time = clampedTime;
        _audioSource.volume = powerDepletedVolume;
        _audioSource.loop = false;
        _audioSource.Play();
    }

    public void SetTimeInterval(float interval)
    {
        timeInterval = interval;
    }
}
