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

    private bool isUiVisible = false;
    private int currentPowerLevel = 4;
    private float timer = 0f;
    private bool countdownStarted = false;
    private GameObject[] powerObjects;
    private GameObject[] signalObjects;
    private bool isFirstPerson = false;
    private Coroutine showCoroutine;

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
    }

    void Update()
    {
        bool currentFirstPerson = IsFirstPersonMode();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleUi();
        }

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
                EndGame();
            }
        }
    }

    private void EndGame()
    {
        countdownStarted = false;
        Debug.Log("游戏结束！");
    }

    public void SetTimeInterval(float interval)
    {
        timeInterval = interval;
    }
}
