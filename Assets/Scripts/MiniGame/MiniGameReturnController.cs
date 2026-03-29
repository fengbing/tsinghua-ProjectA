using UnityEngine;
using UnityEngine.UI;

/// <summary>挂在「返回」按钮上；点击后卸载叠加小游戏场景并回到主场景机位。</summary>
[RequireComponent(typeof(Button))]
public class MiniGameReturnController : MonoBehaviour
{
    void OnEnable() => GetComponent<Button>().onClick.AddListener(ReturnToWorld);

    void OnDisable() => GetComponent<Button>().onClick.RemoveListener(ReturnToWorld);

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            ReturnToWorld();
    }

    public void ReturnToWorld() => MiniGameAdditiveFlow.EndAndRestoreWorld();
}
