using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 让无人机的螺旋桨持续旋转。
/// 若模型是「多个子物体拼成螺旋桨部件」而不是「4 个子物体各代表 1 个桨」，用「整体旋转」。
/// </summary>
public class PropellerSpin : MonoBehaviour
{
    [Header("整体旋转（推荐：部件拆成多个子物体时用这个）")]
    [Tooltip("螺旋桨的每个部分拆成多个子物体时，把装这些部件的父物体拖进来，整组会一起转")]
    [SerializeField] Transform singlePropellerRoot;

    [Header("或：按个指定要转的物体")]
    [Tooltip("四个螺旋桨各是一个子物体时，拖那个父物体")]
    [SerializeField] Transform propellerGroup;
    [Tooltip("或手动把要转的多个 Transform 拖进数组")]
    [SerializeField] Transform[] propellers;

    [Header("转速与转轴")]
    [SerializeField] float degreesPerSecond = 1200f;
    [Tooltip("螺旋桨绕哪根轴转：看模型里桨叶的转轴方向选")]
    [SerializeField] SpinAxis spinAxis = SpinAxis.LocalY;

    enum SpinAxis { LocalY, LocalZ, LocalX }

    List<Transform> _list = new List<Transform>();

    void Start()
    {
        RefreshList();
    }

    void OnValidate()
    {
        RefreshList();
    }

    void RefreshList()
    {
        _list.Clear();
        if (singlePropellerRoot != null)
        {
            _list.Add(singlePropellerRoot);
            return;
        }
        if (propellerGroup != null)
        {
            for (int i = 0; i < propellerGroup.childCount; i++)
                _list.Add(propellerGroup.GetChild(i));
        }
        else if (propellers != null)
        {
            foreach (Transform t in propellers)
                if (t != null) _list.Add(t);
        }
    }

    void FixedUpdate()
    {
        if (_list.Count == 0) return;

        float delta = degreesPerSecond * Time.fixedDeltaTime;
        Vector3 axis = spinAxis == SpinAxis.LocalZ ? Vector3.forward : (spinAxis == SpinAxis.LocalX ? Vector3.right : Vector3.up);

        for (int i = 0; i < _list.Count; i++)
        {
            if (_list[i] != null)
                _list[i].Rotate(axis, delta, Space.Self);
        }
    }
}
