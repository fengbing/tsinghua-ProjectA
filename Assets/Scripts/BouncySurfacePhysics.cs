using UnityEngine;

/// <summary>
/// 只给「膜 / 承载面」本体的 Collider 赋弹性材质；地面、其它物体不必设弹跳。
/// 开启 <see cref="membraneDominatesBounce"/> 时，弹性混合取较大值，物品用默认无弹材质也会被膜顶起轻微弹跳。
/// Unity 2022：PhysicMaterial；Unity 6+：PhysicsMaterial。
/// </summary>
[DisallowMultipleComponent]
public class BouncySurfacePhysics : MonoBehaviour
{
    [Header("膜专属弹跳")]
    [Tooltip("为真时：弹性混合用 Maximum，主要由本表面 bounciness 决定；物品/地面保持默认材质（弹性 0）即可")]
    [SerializeField] bool membraneDominatesBounce = true;

    [Header("弹性")]
    [Tooltip("仅加在本脚本覆盖的 Collider 上；0~1，轻微弹跳建议 0.08~0.25")]
    [SerializeField, Range(0f, 1f)] float bounciness = 0.15f;

    [Tooltip("与另一碰撞体摩擦；略大可避免一碰就滑走")]
    [SerializeField, Range(0f, 1f)] float dynamicFriction = 0.45f;
    [SerializeField, Range(0f, 1f)] float staticFriction = 0.45f;

    [Tooltip("membraneDominatesBounce 关闭时生效：与另一物体材质的弹性混合方式")]
#if UNITY_6000_0_OR_NEWER
    [SerializeField] PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Maximum;
    [SerializeField] PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Average;
#else
    [SerializeField] PhysicMaterialCombine bounceCombine = PhysicMaterialCombine.Maximum;
    [SerializeField] PhysicMaterialCombine frictionCombine = PhysicMaterialCombine.Average;
#endif

    [Header("目标")]
    [Tooltip("为空则对本物体及子物体上所有非 Trigger 的 Collider 赋值")]
    [SerializeField] Collider[] targets;

#if UNITY_6000_0_OR_NEWER
    PhysicsMaterial _material;
#else
    PhysicMaterial _material;
#endif

    void Awake()
    {
        BuildMaterial();

        if (targets != null && targets.Length > 0)
        {
            foreach (var c in targets)
                Apply(c);
            return;
        }

        foreach (var c in GetComponentsInChildren<Collider>())
        {
            if (c == null || c.isTrigger) continue;
            Apply(c);
        }
    }

    void BuildMaterial()
    {
#if UNITY_6000_0_OR_NEWER
        _material = new PhysicsMaterial($"{name}_BouncySurface");
#else
        _material = new PhysicMaterial($"{name}_BouncySurface");
#endif
        _material.bounciness = bounciness;
        _material.dynamicFriction = dynamicFriction;
        _material.staticFriction = staticFriction;
        _material.bounceCombine = ResolveBounceCombine();
        _material.frictionCombine = frictionCombine;
    }

    void Apply(Collider c) => c.material = _material;

#if UNITY_6000_0_OR_NEWER
    PhysicsMaterialCombine ResolveBounceCombine() =>
        membraneDominatesBounce ? PhysicsMaterialCombine.Maximum : bounceCombine;
#else
    PhysicMaterialCombine ResolveBounceCombine() =>
        membraneDominatesBounce ? PhysicMaterialCombine.Maximum : bounceCombine;
#endif

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying || _material == null) return;
        _material.bounciness = bounciness;
        _material.dynamicFriction = dynamicFriction;
        _material.staticFriction = staticFriction;
        _material.bounceCombine = ResolveBounceCombine();
        _material.frictionCombine = frictionCombine;
    }
#endif
}
