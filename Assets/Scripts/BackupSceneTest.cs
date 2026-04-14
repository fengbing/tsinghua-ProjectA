using UnityEngine;

/// <summary>
/// Backup 场景测试脚本：自动触发对应弹窗，方便测试射线检测和按钮绑定。
/// 正式发布时删除或禁用此组件。
/// </summary>
public class BackupSceneTest : MonoBehaviour
{
    [Header("自动测试（启动后 N 秒自动触发）")]
    [Tooltip("启动后几秒自动弹出成功弹窗，设为 0 则禁用")]
    [SerializeField] private float autoShowSuccessDelay = 2f;
    [Tooltip("启动后几秒自动弹出损坏弹窗，设为 0 则禁用")]
    [SerializeField] private float autoShowBrokenDelay = 0f;
    [Tooltip("启动后几秒自动弹出电量耗尽弹窗，设为 0 则禁用")]
    [SerializeField] private float autoShowPowerDepletedDelay = 0f;

    void Start()
    {
        if (autoShowSuccessDelay > 0)
            Invoke(nameof(TestSuccess), autoShowSuccessDelay);
        if (autoShowBrokenDelay > 0)
            Invoke(nameof(TestBroken), autoShowBrokenDelay);
        if (autoShowPowerDepletedDelay > 0)
            Invoke(nameof(TestPowerDepleted), autoShowPowerDepletedDelay);
    }

    void TestSuccess()
    {
        Debug.Log("[BackupSceneTest] → ShowSuccessDialog()");
        BackupDialogEvents.Instance?.ShowSuccessDialog();
    }

    void TestBroken()
    {
        Debug.Log("[BackupSceneTest] → ShowBrokenDialog()");
        BackupDialogEvents.Instance?.ShowBrokenDialog();
    }

    void TestPowerDepleted()
    {
        Debug.Log("[BackupSceneTest] → ShowPowerDepletedDialog()");
        BackupDialogEvents.Instance?.ShowPowerDepletedDialog();
    }
}
