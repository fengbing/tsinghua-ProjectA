using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DecryptPuzzleUI : MonoBehaviour
{
    [Header("密码图案显示（4个）- 显示正确答案的图案")]
    [SerializeField] private Image[] codePatternDisplays = new Image[DecryptPuzzleSystem.CodeLength];

    [Header("玩家输入显示（4个）- TextMeshPro显示输入的数字")]
    [SerializeField] private TextMeshProUGUI[] inputDigitDisplays = new TextMeshProUGUI[DecryptPuzzleSystem.CodeLength];

    [Header("数字0-9的Sprite图案")]
    [SerializeField] private Sprite[] digitSprites = new Sprite[10];

    [Header("反馈文本")]
    [SerializeField] private TextMeshProUGUI feedbackText;

    [Header("面板根物体")]
    [SerializeField] private GameObject panelRoot;

    [Header("核心系统")]
    [SerializeField] private DecryptPuzzleSystem puzzleSystem;

    [Header("特效切换")]
    [SerializeField] private GameObject effectTransition;

    [Header("答对后显示的对象（答对前隐藏）")]
    [SerializeField] private GameObject[] objectsToShowOnSuccess;

    public static DecryptPuzzleUI Instance { get; private set; }

    /// <summary>
    /// 解谜成功时触发的事件（外部脚本可订阅）
    /// </summary>
    public event Action OnDecryptPuzzleSolved;

    private bool _planeInputWasEnabled = true;
    private PlaneController _planeController;

    void Awake()
    {
        if (Instance == null)
            Instance = this;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        // 答对前隐藏 objectsToShowOnSuccess
        if (objectsToShowOnSuccess != null)
        {
            foreach (var obj in objectsToShowOnSuccess)
            {
                if (obj != null) obj.SetActive(false);
            }
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        // 答对前隐藏 objectsToShowOnSuccess
        if (objectsToShowOnSuccess != null)
        {
            foreach (var obj in objectsToShowOnSuccess)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        // 查找 PlaneController
        _planeController = FindObjectOfType<PlaneController>();

        if (puzzleSystem != null)
        {
            puzzleSystem.OnDigitEntered += UpdateInputDisplay;
            puzzleSystem.OnPuzzleSolved += OnPuzzleSolved;
            puzzleSystem.OnPuzzleFailed += OnPuzzleFailed;
            puzzleSystem.OnCodeGenerated += UpdateCodeDisplay;
            puzzleSystem.Initialize();
        }
    }

    void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf)
            return;

        // 数字键输入 (0-9)
        for (int i = 0; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
            {
                OnNumberPressed(i);
                return;
            }
        }

        // 确认键 (Enter)
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            OnConfirmPressed();
            return;
        }

        // 退格键 (Backspace)
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            OnBackspacePressed();
            return;
        }
    }

    void OnNumberPressed(int digit)
    {
        puzzleSystem?.TryEnterDigit(digit);
    }

    void OnConfirmPressed()
    {
        if (puzzleSystem != null)
        {
            puzzleSystem.TryConfirm();
        }
    }

    void OnBackspacePressed()
    {
        if (puzzleSystem != null)
        {
            puzzleSystem.Backspace();
            ShowFeedback("已删除");
        }
    }

    /// <summary>
    /// 显示密码的图案（正确答案）
    /// </summary>
    void UpdateCodeDisplay()
    {
        if (puzzleSystem == null)
            return;

        int[] codeDigits = puzzleSystem.CorrectCodeDigits;
        for (int i = 0; i < codePatternDisplays.Length; i++)
        {
            if (codePatternDisplays[i] == null)
                continue;

            if (codeDigits != null && i < codeDigits.Length)
            {
                SetDigitImage(codePatternDisplays[i], codeDigits[i]);
            }
            else
            {
                ClearDigitImage(codePatternDisplays[i]);
            }
        }
    }

    /// <summary>
    /// 显示玩家输入的数字
    /// </summary>
    void UpdateInputDisplay(int filledCount)
    {
        if (puzzleSystem == null)
            return;

        for (int i = 0; i < inputDigitDisplays.Length; i++)
        {
            if (inputDigitDisplays[i] == null)
                continue;

            if (i < filledCount && filledCount > 0)
            {
                inputDigitDisplays[i].text = puzzleSystem.CurrentInput[i].ToString();
            }
            else
            {
                inputDigitDisplays[i].text = "";
            }
        }
    }

    void SetDigitImage(Image image, int digit)
    {
        if (digit >= 0 && digit < digitSprites.Length && digitSprites[digit] != null)
        {
            image.sprite = digitSprites[digit];
            image.enabled = true;
        }
    }

    void ClearDigitImage(Image image)
    {
        image.sprite = null;
        image.enabled = false;
    }

    void OnPuzzleSolved()
    {
        Debug.Log("[UI] OnPuzzleSolved 被调用！");

        // 立刻显示反馈
        ShowFeedback("取件码正确！");

        // 延迟1秒后同时显示物体/特效和隐藏面板
        StartCoroutine(ShowSuccessAndHideRoutine());
    }

    System.Collections.IEnumerator ShowSuccessAndHideRoutine()
    {
        yield return new WaitForSecondsRealtime(1f);

        // 显示物体
        if (objectsToShowOnSuccess != null)
        {
            foreach (var obj in objectsToShowOnSuccess)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        OnDecryptPuzzleSolved?.Invoke();
        if (effectTransition != null)
            effectTransition.SendMessage("ShowEffect1");

        // 同时隐藏面板并恢复控制
        HidePanelAndRestoreControl();
    }

    void HidePanelAndRestoreControl()
    {
        Debug.Log("[UI] HidePanelAndRestoreControl 被调用");

        // 隐藏面板
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
            Debug.Log("[UI] panelRoot 已隐藏");
        }

        // 恢复游戏时间
        Time.timeScale = 1f;

        // 恢复 Plane 输入
        if (_planeController != null)
        {
            _planeController.SetInputEnabled(true);
            Debug.Log("[UI] Plane 输入已恢复");
        }
    }

    void OnPuzzleFailed()
    {
        ShowFeedback("取件码错误！");
        UpdateInputDisplay(0);
    }

    void ShowFeedback(string message)
    {
        if (feedbackText != null)
            feedbackText.text = message;
    }

    void HidePanel()
    {
        Debug.Log("[UI] HidePanel 被调用");
        Hide();
    }

    public void Show()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);

            // 暂停游戏
            Time.timeScale = 0f;

            // 禁用 Plane 输入，但保持鼠标锁定
            if (_planeController != null)
            {
                _planeInputWasEnabled = _planeController.IsInputEnabled;
                _planeController.SetInputEnabled(false);
            }

            // 刷新显示
            UpdateCodeDisplay();
            UpdateInputDisplay(0);
        }
    }

    public void Hide()
    {
        Debug.Log($"[UI] Hide 调用, panelRoot={panelRoot?.name}");
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
            Debug.Log($"[UI] panelRoot 已隐藏");

            // 隐藏 objectsToShowOnSuccess
            if (objectsToShowOnSuccess != null)
            {
                foreach (var obj in objectsToShowOnSuccess)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }
        }

        // 恢复游戏时间
        Time.timeScale = 1f;

        // 恢复 Plane 输入
        if (_planeController != null)
            _planeController.SetInputEnabled(true);
    }

    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    void OnDisable()
    {
        if (puzzleSystem != null)
        {
            puzzleSystem.ClearInput();
        }
    }
}
