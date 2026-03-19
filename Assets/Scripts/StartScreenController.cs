using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 进入游戏的开始画面：显示图片 + 全屏隐形按钮，点击后隐藏画面或加载游戏场景。
/// 用法：把开始画面图片拖到 Panel 下的 Image，把本脚本挂到 Panel 上，把「进入游戏」按钮拖到 Start Button。
/// </summary>
public class StartScreenController : MonoBehaviour
{
    [Tooltip("开始画面所在 Panel（挂本脚本的物体或父物体）")]
    [SerializeField] GameObject startScreenPanel;
    [Tooltip("点击后进入游戏的全屏隐形按钮")]
    [SerializeField] Button startButton;
    [Tooltip("留空则只隐藏开始画面；填场景名则切换场景，如 PlaneGame")]
    [SerializeField] string gameSceneName;

    bool _loading;

    void Awake()
    {
        if (startScreenPanel == null)
            startScreenPanel = gameObject;
        if (startButton == null)
            startButton = GetComponentInChildren<Button>();
    }

    void Start()
    {
        if (startScreenPanel != null)
            startScreenPanel.SetActive(true);

        if (startButton != null)
            startButton.onClick.AddListener(OnStartGame);
    }

    void Update()
    {
        if (_loading) return;
        if (startScreenPanel != null && !startScreenPanel.activeSelf) return;
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            OnStartGame();
    }

    public void OnStartGame()
    {
        if (_loading) return;

        if (!string.IsNullOrEmpty(gameSceneName))
        {
            _loading = true;
            StartCoroutine(LoadGameSceneAsync());
        }
        else if (startScreenPanel != null)
        {
            startScreenPanel.SetActive(false);
        }
    }

    IEnumerator LoadGameSceneAsync()
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName);
        if (op == null) yield break;
        op.allowSceneActivation = true;
        while (!op.isDone)
            yield return null;
    }
}
