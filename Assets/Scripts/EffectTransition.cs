using UnityEngine;

public class EffectTransition : MonoBehaviour
{
    [Header("目标场景")]
    [SerializeField] private string targetScene;

    [Header("第一个特效")]
    [SerializeField] private GameObject effect1;

    [Header("第二个特效")]
    [SerializeField] private GameObject effect2;

    [Header("Loading 屏幕")]
    [Tooltip("留空则自动查找 StorageLoadingScreen 单例")]
    [SerializeField] private StorageLoadingScreen loadingScreen;

    private bool _triggered;

    void Start()
    {
        Debug.Log("[EffectTransition] Start");
        if (effect1 != null) effect1.SetActive(false);
        if (effect2 != null) effect2.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[EffectTransition] OnTriggerEnter: triggered={_triggered}");
        if (_triggered) return;
        var gripper = other.GetComponentInChildren<DroneGripper>();
        Debug.Log($"[EffectTransition] Gripper: {(gripper != null ? "找到" : "未找到")}");
        if (gripper == null) return;
        _triggered = true;

        // 强制释放箱子
        gripper.ForceRelease();

        // 查找 Loading 屏幕
        var screen = loadingScreen != null ? loadingScreen : StorageLoadingScreen.Instance;
        Debug.Log($"[EffectTransition] Screen: {(screen != null ? $"找到 targetScene={targetScene}" : "未找到，回退直接加载")}");

        if (screen != null)
        {
            screen.BeginLoadingSequence(targetScene);
        }
        else
        {
            Debug.LogWarning("[EffectTransition] 未找到 StorageLoadingScreen，回退为直接同步加载。");
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
        }
    }

    public void ShowEffect1()
    {
        Debug.Log("[EffectTransition] ShowEffect1");
        if (effect1 != null)
            effect1.SetActive(true);
    }

    public void ShowEffect2()
    {
        Debug.Log("[EffectTransition] ShowEffect2");
        if (effect1 != null) effect1.SetActive(false);
        if (effect2 != null) effect2.SetActive(true);
    }
}
