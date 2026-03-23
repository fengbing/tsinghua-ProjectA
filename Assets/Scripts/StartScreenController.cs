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
    [Tooltip("加载进度条")]
    [SerializeField] Slider loadingSlider;
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

        if (loadingSlider != null)
            loadingSlider.gameObject.SetActive(false);

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

            if (loadingSlider != null)
                loadingSlider.gameObject.SetActive(true);

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
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            if (loadingSlider != null)
                loadingSlider.value = op.progress;
            yield return null;
        }

        if (loadingSlider != null)
            loadingSlider.value = 1f;

        yield return new WaitForSeconds(0.1f);
        op.allowSceneActivation = true;
    }
}
