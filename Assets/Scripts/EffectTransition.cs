using UnityEngine;
using UnityEngine.SceneManagement;

public class EffectTransition : MonoBehaviour
{
    [Header("目标场景")]
    [SerializeField] private string targetScene;

    [Header("第一个特效")]
    [SerializeField] private GameObject effect1;

    [Header("第二个特效")]
    [SerializeField] private GameObject effect2;

    private bool _triggered;

    void Start()
    {
        if (effect1 != null) effect1.SetActive(false);
        if (effect2 != null) effect2.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        var gripper = other.GetComponentInChildren<DroneGripper>();
        if (gripper == null) return;
        _triggered = true;
        gripper.PrepareForSceneTransition(targetScene);
    }

    public void ShowEffect1()
    {
        if (effect1 != null)
            effect1.SetActive(true);
    }

    public void ShowEffect2()
    {
        if (effect1 != null) effect1.SetActive(false);
        if (effect2 != null) effect2.SetActive(true);
    }
}
