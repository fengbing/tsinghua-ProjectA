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
    [Tooltip("旋转中心：TransformPivot 用物体自身 pivot；GeometryCenter 用渲染几何中心")]
    [SerializeField] PivotMode pivotMode = PivotMode.TransformPivot;

    enum SpinAxis { LocalY, LocalZ, LocalX }
    enum PivotMode { TransformPivot, GeometryCenter }

    struct SpinTarget
    {
        public Transform transform;
        public Renderer[] renderers;
    }

    List<SpinTarget> _targets = new List<SpinTarget>();

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
        _targets.Clear();
        if (singlePropellerRoot != null)
        {
            AddTarget(singlePropellerRoot);
            return;
        }
        if (propellerGroup != null)
        {
            for (int i = 0; i < propellerGroup.childCount; i++)
                AddTarget(propellerGroup.GetChild(i));
        }
        else if (propellers != null)
        {
            foreach (Transform t in propellers)
                if (t != null) AddTarget(t);
        }
    }

    void AddTarget(Transform t)
    {
        _targets.Add(new SpinTarget
        {
            transform = t,
            renderers = t.GetComponentsInChildren<Renderer>(true)
        });
    }

    bool TryGetGeometryCenter(SpinTarget target, out Vector3 center)
    {
        center = target.transform.position;
        Renderer[] renderers = target.renderers;
        if (renderers == null || renderers.Length == 0) return false;

        bool hasAny = false;
        Bounds b = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            if (!hasAny)
            {
                b = r.bounds;
                hasAny = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        if (!hasAny) return false;
        center = b.center;
        return true;
    }

    void FixedUpdate()
    {
        if (_targets.Count == 0) return;

        float delta = degreesPerSecond * Time.fixedDeltaTime;
        Vector3 axis = spinAxis == SpinAxis.LocalZ ? Vector3.forward : (spinAxis == SpinAxis.LocalX ? Vector3.right : Vector3.up);

        for (int i = 0; i < _targets.Count; i++)
        {
            SpinTarget target = _targets[i];
            if (target.transform == null) continue;

            if (pivotMode == PivotMode.GeometryCenter && TryGetGeometryCenter(target, out Vector3 center))
            {
                Vector3 worldAxis = target.transform.TransformDirection(axis);
                target.transform.RotateAround(center, worldAxis, delta);
            }
            else
            {
                target.transform.Rotate(axis, delta, Space.Self);
            }
        }
    }
}
