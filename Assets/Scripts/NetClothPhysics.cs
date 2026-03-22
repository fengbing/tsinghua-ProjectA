using UnityEngine;

/// <summary>
/// 网兜 / 张拉膜 Cloth 物理：略带弹性、能接住刚体包裹。
/// 需在膜 Mesh 上挂 Cloth，四角用约束或 <see cref="NetClothAutoCorners"/> 固定。
/// 注意：Cloth 只与注册的 Sphere / Capsule 碰撞体作用，纯 BoxCollider 不会顶起布料，请在包裹上增加子物体球体或胶囊。
/// </summary>
[RequireComponent(typeof(Cloth))]
public class NetClothPhysics : MonoBehaviour
{
    [Header("整体手感")]
    [Tooltip("拉伸刚度：越小膜越“软”、下垂与弹性感越明显（约 0.35~0.75）")]
    [SerializeField, Range(0f, 1f)] float stretchingStiffness = 0.55f;
    [Tooltip("弯曲刚度：越小布面越易弯折")]
    [SerializeField, Range(0f, 1f)] float bendingStiffness = 0.2f;
    [Tooltip("阻尼：越大摆动越快停住，略弹但不一直抖")]
    [SerializeField, Range(0f, 1f)] float damping = 0.45f;
    [Tooltip("与碰撞体摩擦，防止物体一碰就滑穿")]
    [SerializeField, Range(0f, 1f)] float friction = 0.65f;
    [SerializeField] bool useGravity = true;

    [Header("与刚体碰撞时的“重量感”")]
    [Tooltip("与刚体碰撞时粒子等效质量倍率；略大于 1 时物品压膜的下陷与回弹更明显")]
    [SerializeField, Min(0f)] float collisionMassScale = 1f;
    [Tooltip("膜所在物体移动/旋转时，带动顶点速度的比例（无人机带着膜飞时可略调大）")]
    [SerializeField, Range(0f, 1f)] float worldVelocityScale = 1f;
    [Tooltip("膜所在物体加速度对顶点的影响（一般保持默认即可）")]
    [SerializeField, Range(0f, 1f)] float worldAccelerationScale = 1f;

    [Header("稳定性（物品快速放下时建议开启）")]
    [Tooltip("连续碰撞，减轻高速物体穿透膜")]
    [SerializeField] bool enableContinuousCollision = true;
    [Tooltip("虚拟粒子强度（Cloth API 为 float）：0 关闭，1 为每个三角一个虚拟粒子，碰撞更稳")]
    [SerializeField, Range(0f, 1f)] float useVirtualParticles = 1f;

    [Header("与包裹 / 无人机碰撞")]
    [Tooltip("参与布料碰撞的 SphereCollider（包裹上的球体或简化碰撞）")]
    [SerializeField] SphereCollider[] packageSphereColliders;
    [Tooltip("参与布料碰撞的 CapsuleCollider（机械臂、机身简化为胶囊时可直接拖）")]
    [SerializeField] CapsuleCollider[] packageCapsuleColliders;
    [Tooltip("是否在运行时合并进 Cloth 的碰撞列表")]
    [SerializeField] bool registerSpheresOnEnable = true;

    Cloth _cloth;

    void Awake() => _cloth = GetComponent<Cloth>();

    void OnEnable()
    {
        if (_cloth == null) _cloth = GetComponent<Cloth>();
        ApplySimulationSettings();
        if (registerSpheresOnEnable) RegisterPackageColliders();
    }

    /// <summary>应用拉伸/弯曲/阻尼及与刚体碰撞相关参数。</summary>
    public void ApplySimulationSettings()
    {
        if (_cloth == null) return;
        _cloth.stretchingStiffness = stretchingStiffness;
        _cloth.bendingStiffness = bendingStiffness;
        _cloth.damping = damping;
        _cloth.friction = friction;
        _cloth.useGravity = useGravity;
        _cloth.collisionMassScale = collisionMassScale;
        _cloth.worldVelocityScale = worldVelocityScale;
        _cloth.worldAccelerationScale = worldAccelerationScale;
        _cloth.enableContinuousCollision = enableContinuousCollision;
        _cloth.useVirtualParticles = useVirtualParticles;
    }

    /// <summary>把包裹/无人机上的球体与胶囊注册进 Cloth。</summary>
    public void RegisterPackageColliders()
    {
        RegisterSphereColliders();
        RegisterCapsuleColliders();
    }

    /// <summary>仅合并球体碰撞（兼容旧调用名）。</summary>
    public void RegisterSphereColliders() => MergeSphereCollidersFrom(packageSphereColliders);

    /// <summary>运行时向膜注册额外球体（例如动态生成的包裹）。</summary>
    public void MergeSphereCollidersFrom(SphereCollider[] spheres)
    {
        if (_cloth == null || spheres == null || spheres.Length == 0) return;

        var list = new System.Collections.Generic.List<ClothSphereColliderPair>();
        if (_cloth.sphereColliders != null)
            list.AddRange(_cloth.sphereColliders);

        for (int i = 0; i < spheres.Length; i++)
        {
            var s = spheres[i];
            if (s == null) continue;
            list.Add(new ClothSphereColliderPair(s, null));
        }

        _cloth.sphereColliders = list.ToArray();
    }

    void RegisterCapsuleColliders()
    {
        if (_cloth == null || packageCapsuleColliders == null || packageCapsuleColliders.Length == 0)
            return;

        var list = new System.Collections.Generic.List<CapsuleCollider>();
        if (_cloth.capsuleColliders != null)
            list.AddRange(_cloth.capsuleColliders);

        for (int i = 0; i < packageCapsuleColliders.Length; i++)
        {
            var c = packageCapsuleColliders[i];
            if (c == null) continue;
            list.Add(c);
        }

        _cloth.capsuleColliders = list.ToArray();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (_cloth == null) _cloth = GetComponent<Cloth>();
        ApplySimulationSettings();
        if (registerSpheresOnEnable) RegisterPackageColliders();
    }
#endif
}
