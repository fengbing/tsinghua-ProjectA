using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 按 F 抓取/放下：播放爪子动画，并用 FixedJoint 真实连接箱子 Rigidbody。
/// 约定：
/// - 抓取点为子物体 GrabPoint（可自动创建/查找）
/// - 抓取触发器为 GrabPoint 子物体 GrabTrigger（SphereCollider/IsTrigger）
/// - 目标箱子需有 Rigidbody + Grabbable
/// </summary>
public class DroneGripper : MonoBehaviour
{
    [SerializeField] KeyCode grabKey = KeyCode.F;

    [Header("动画（可不填）")]
    [SerializeField] Animator clawAnimator;
    [SerializeField] string grabTriggerName = "Grab";
    [SerializeField] string releaseTriggerName = "Release";
    [SerializeField] float attachDelay = 0.25f;
    [Tooltip("若 Animator 参数名拼错（例如 Relsase），可在这里填备用 Trigger 名称")]
    [SerializeField] string releaseTriggerFallbackName = "Relsase";

    [Header("抓取点")]
    [SerializeField] Transform grabPoint;

    [Header("抓取点自动居中")]
    [SerializeField] bool autoRecenterGrabPoint = false;
    [Tooltip("用于计算爪瓣中心：取抓取根节点下所有 Renderer 的 bounds.center 平均值")]
    [SerializeField] bool useRenderersBounds = true;
    [Tooltip("当 useRenderersBounds=false 时，改用 Collider 的 bounds.center 平均值")]
    [SerializeField] bool useCollidersBounds = false;
    [Tooltip("直接使用 GrabPoint 的 Transform.position 作为抓取中心；当爪子层级很复杂或带有额外 Collider 时，推荐开启")]
    [SerializeField] bool useGrabPointTransformPosition = true;

    [Header("对齐策略")]
    [Tooltip("抓取时用“箱子碰撞体上，离抓取中心最近的点”来对齐爪子中心；适合从边/底抓取避免贴边穿模")]
    [SerializeField] bool useClosestPointAlignment = true;
    [Tooltip("当抓取中心位于箱子 Collider 内部时，ClosestPoint 可能等于抓取中心导致 delta=0。此阈值内会回退到箱子几何中心对齐。单位：米")]
    [SerializeField] float closestPointInsideEpsilon = 0.01f;
    [Tooltip("开启后在抓取对齐时打印关键计算值（控制台）。")]
    [SerializeField] bool debugAlignment = false;

    [Header("Joint 参数")]
    [SerializeField] float breakForce = Mathf.Infinity;
    [SerializeField] float breakTorque = Mathf.Infinity;
    [Tooltip("降低载荷对无人机的拖累（越小越不影响飞行，但越不“真实”）")]
    [SerializeField, Range(0.05f, 2f)] float payloadMassScale = 0.25f;

    [Header("防穿模（高速释放时）")]
    [SerializeField] bool enableAntiTunneling = true;
    [SerializeField] float releaseUpOffset = 0.15f;
    [SerializeField] float releaseMaxSpeed = 25f;
    [SerializeField] float postReleaseCcdSeconds = 1.0f;

    [Header("抓取对齐保护")]
    [Tooltip("抓取瞬移/对齐后的短时间内关闭箱子碰撞，避免被环境立刻挤回原位（单位：秒）")]
    [SerializeField] float attachCollisionDisableSeconds = 0.20f;

    [Header("持有时锚点修正（强制对齐）")]
    [Tooltip("holding 状态下：根据 FixedJoint.anchor 的世界坐标与 GrabPoint 中心的差值，强制修正箱子位置，保证视觉上对齐。")]
    [SerializeField] bool enableAnchorTranslationCorrection = true;
    [Tooltip("忽略小于该平方距离的对齐误差（单位：米^2）。")]
    [SerializeField] float anchorTranslationCorrectionMinSqr = 1e-6f;

    [Header("抓取时序")]
    [Tooltip("如果开启：按 F 只播放爪子动画，等 Animator 动画通过 Animation Event 调用 AttachNowFromAnimationEvent() 后才真正抓取。")]
    [SerializeField] bool attachViaAnimationEvent = false;

    [Header("IdleOpen 等待抓取")]
    [Tooltip("如果开启：按 F 后停止无人机输入，并等待 Animator 进入“open 动作播放完毕后的那个状态”才真正抓取。")]
    [SerializeField] bool attachWhenIdleOpen = true;
    [Tooltip("Animator 当前状态名为此字符串时认为进入“open 动作播放完毕”。（请填 open 动作结束后进入的状态名）")]
    [SerializeField] string idleOpenStateName = "IdleOpen";
    [Tooltip("读取 Animator 状态的层级索引。通常为 0。")]
    [SerializeField] int idleOpenStateLayer = 0;
    [Tooltip("等待 IdleOpen 的最长秒数；超时会直接附着（避免卡死）。")]
    [SerializeField] float idleOpenMaxWaitSeconds = 3f;
    [Tooltip("等待 IdleOpen 期间是否禁用 PlaneController 输入。")]
    [SerializeField] bool stopPlaneInputWhileWaiting = true;

    [Header("IdleOpen 进入判定")]
    [Tooltip("只有当进入 HoldOpen 后的 normalizedTime 分数部分 <= 该值，才认为“刚进入完成点”。例如 0.1 表示刚进入的前 10% 帧。")]
    [SerializeField] float desiredStateEnterNormalizedFracMax = 0.15f;
    [Tooltip("用于等待期间记录按 F 当帧 Animator 的 stateHash，避免在未真正播放 open 时就因初始已处于 HoldOpen 而立即抓取。")]
    [SerializeField] bool usePressStateHashGuard = true;

    [Header("特效切换")]
    [SerializeField] private GameObject effectTransition;

    [Header("跨场景抓取恢复")]
    [Tooltip("切换场景后恢复抓取时，用此名称在新场景中查找同名包裹对象")]
    [SerializeField] string carriedPackageName = "Package";

    // --- Runtime state ---
    Rigidbody _droneRb;
    bool _wasHoldingBeforeSceneLoad;
    Vector3 _carriedRelativeOffset;
    Grabbable _candidate;
    Grabbable _holding;
    FixedJoint _joint;
    float _attachAt;
    bool _pendingAttach;

    CollisionDetectionMode _prevCcd;
    RigidbodyInterpolation _prevInterp;
    bool _savedRbSettings;
    bool _prevBoxUseGravity;
    bool _savedBoxGravity;
    RigidbodyConstraints _prevBoxConstraints;
    bool _savedBoxConstraints;
    bool _savedBoxDetectCollisions;
    bool _prevBoxDetectCollisions;
    float _restoreCollisionsAtTime;
    bool _savedBoxIsKinematic;
    bool _prevBoxIsKinematic;
    int _debugFramesRemaining;

    // 当使用 Animation Event 延迟抓取时，保存按 F 时的目标，
    // 避免动画过程中触发器离开导致 _candidate 被清空。
    Grabbable _scheduledCandidate;

    PlaneController _planeController;
    bool _waitingForIdleOpen;
    float _idleOpenDeadline;
    int _animStateHashAtPress;

    struct ColliderPair
    {
        public Collider a;
        public Collider b;

        public ColliderPair(Collider a, Collider b)
        {
            this.a = a;
            this.b = b;
        }
    }

    readonly System.Collections.Generic.List<ColliderPair> _ignoredCollisionPairs = new System.Collections.Generic.List<ColliderPair>();

    void Awake()
    {
        _droneRb = GetComponentInParent<Rigidbody>();
        if (_droneRb == null) _droneRb = GetComponent<Rigidbody>();

        if (clawAnimator == null) clawAnimator = GetComponentInChildren<Animator>();
        if (grabPoint == null)
        {
            var t = transform.Find("GrabPoint");
            if (t != null) grabPoint = t;
            else
            {
                var go = new GameObject("GrabPoint");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                grabPoint = go.transform;
            }
        }

        // 你不需要运行时自动居中；避免在 Play 中修改 GrabPoint 位置导致抓取点偏差与抖动。
        // 仅允许在编辑状态（非 Play）且你手动开启时才执行一次。
#if UNITY_EDITOR
        if (!Application.isPlaying && autoRecenterGrabPoint)
            RecenterGrabPoint();
#endif

        _planeController = GetComponentInParent<PlaneController>();
    }

    Animator GetAnimator()
    {
        if (clawAnimator != null) return clawAnimator;
        clawAnimator = GetComponentInChildren<Animator>();
        return clawAnimator;
    }

    bool HasTriggerParameter(Animator anim, string parameterName)
    {
        if (anim == null) return false;
        if (string.IsNullOrEmpty(parameterName)) return false;

        // Animator.parameters 在运行时可遍历并判断类型
        // 目的：避免对不存在的参数调用 SetTrigger 导致报错刷屏。
        var ps = anim.parameters;
        for (int i = 0; i < ps.Length; i++)
        {
            var p = ps[i];
            if (p == null) continue;
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == parameterName)
                return true;
        }
        return false;
    }

    void TrySetTrigger(Animator anim, string parameterName)
    {
        if (HasTriggerParameter(anim, parameterName))
            anim.SetTrigger(parameterName);
    }

    void Update()
    {
        if (Input.GetKeyDown(grabKey))
        {
            if (_holding != null)
            {
                BeginRelease();
                return;
            }

            if (_waitingForIdleOpen) return; // 等待 IdleOpen 期间不重复触发
            BeginGrab();
        }
    }

    void FixedUpdate()
    {
        if (_waitingForIdleOpen)
        {
            Animator anim = GetAnimator();
            if (anim != null)
            {
                var stateInfo = anim.GetCurrentAnimatorStateInfo(idleOpenStateLayer);
                bool inIdleOpen = stateInfo.IsName(idleOpenStateName);
                float normalizedFrac = stateInfo.normalizedTime - Mathf.Floor(stateInfo.normalizedTime);
                bool justEnteredByFrac = normalizedFrac <= desiredStateEnterNormalizedFracMax;
                bool pressStateGuardOk = !usePressStateHashGuard || stateInfo.shortNameHash != _animStateHashAtPress;
                bool canAttach = inIdleOpen && !anim.IsInTransition(idleOpenStateLayer) && justEnteredByFrac && pressStateGuardOk;
                bool timeout = Time.time >= _idleOpenDeadline;
                if (canAttach || timeout)
                {
                    _waitingForIdleOpen = false;
                    if (stopPlaneInputWhileWaiting)
                        _planeController?.SetInputEnabled(true);

                    // 真正抓取
                    AttachNow();
                }
            }
            else
            {
                // Animator 为空：直接走超时兜底
                if (Time.time >= _idleOpenDeadline)
                {
                    _waitingForIdleOpen = false;
                    if (stopPlaneInputWhileWaiting)
                        _planeController?.SetInputEnabled(true);
                    AttachNow();
                }
            }
        }

        if (_pendingAttach && Time.time >= _attachAt)
        {
            _pendingAttach = false;
            AttachNow();
        }

        if (_holding != null && _savedBoxDetectCollisions && Time.time >= _restoreCollisionsAtTime)
        {
            Rigidbody boxRbRestore = _holding.Rigidbody;
            if (boxRbRestore != null)
            {
                boxRbRestore.detectCollisions = _prevBoxDetectCollisions;
            }
            _savedBoxDetectCollisions = false;
        }

        if (debugAlignment && _holding != null && _joint != null && _debugFramesRemaining > 0 && _droneRb != null)
        {
            Vector3 attachWorldDebug = GetGrabPointCenterWorld();
            Rigidbody boxRbDebug = _holding.Rigidbody;
            if (boxRbDebug != null)
            {
                Vector3 boxPos = boxRbDebug.position;
                Vector3 diff = attachWorldDebug - boxPos;
                Debug.Log(
                    $"[DroneGripper][debug follow] attachWorld={attachWorldDebug} boxPos={boxPos} diff=({diff}) remaining={_debugFramesRemaining}");
            }
            _debugFramesRemaining--;
        }

        // 爪子是动画/机械臂驱动时，GrabPoint 在 attach 后可能仍会移动。
        // FixedJoint 的 anchor/connectedAnchor 默认只在创建时计算一次，
        // 所以这里持续更新，确保箱子约束点始终落在“当前爪子中心”。
        if (_holding == null || _joint == null) return;
        if (_droneRb == null) return;

        Vector3 attachWorld = GetGrabPointCenterWorld();
        Rigidbody boxRb = _holding.Rigidbody;
        if (boxRb == null) return;

        // 关键：FixedJoint 的 `anchor` 是在“箱子局部坐标”里的固定点。
        // 如果每帧都重设 anchor，会让关节约束被脚本“重定义”从而不需要推动箱子移动。
        // 正确做法：只更新 connectedAnchor（抓取点相对无人机刚体的变化），anchor 保持不变。
        _joint.autoConfigureConnectedAnchor = false;
        _joint.connectedAnchor = _droneRb.transform.InverseTransformPoint(attachWorld);

        bool doCorrection = enableAnchorTranslationCorrection || debugAlignment;
        if (doCorrection)
        {
            // 以锚点为准：anchor 世界坐标应始终贴着 GrabPoint 中心。
            Vector3 anchorWorldNow = boxRb.transform.TransformPoint(_joint.anchor);
            Vector3 correction = attachWorld - anchorWorldNow;
            if (debugAlignment)
            {
                Debug.Log(
                    $"[DroneGripper][anchor correction] attachWorld={attachWorld} anchorWorldNow={anchorWorldNow} " +
                    $"correction={correction} correctionMag={correction.magnitude} minSqr={anchorTranslationCorrectionMinSqr} " +
                    $"boxPos(before)={boxRb.position}");
            }

            if (correction.sqrMagnitude > anchorTranslationCorrectionMinSqr)
            {
                boxRb.position += correction;
                boxRb.velocity = Vector3.zero;
                boxRb.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();

                if (debugAlignment)
                {
                    Debug.Log($"[DroneGripper][anchor correction] boxPos(after)={boxRb.position}");
                }
            }
        }
    }

    public void SetCandidate(Grabbable g)
    {
        if (_holding != null) return;
        _candidate = g;
    }

    public void ClearCandidate(Grabbable g)
    {
        if (_candidate == g) _candidate = null;
    }

    void BeginGrab()
    {
        if (_candidate == null || grabPoint == null) return;

        Animator anim = GetAnimator();
        if (anim != null && !string.IsNullOrEmpty(grabTriggerName))
            TrySetTrigger(anim, grabTriggerName);

        if (attachWhenIdleOpen)
        {
            // 等待 Animator 进入 IdleOpen 状态后才真正抓取。
            _scheduledCandidate = _candidate;
            _pendingAttach = false;
            _attachAt = 0f;
            _waitingForIdleOpen = true;
            _idleOpenDeadline = Time.time + Mathf.Max(0f, idleOpenMaxWaitSeconds);

            if (stopPlaneInputWhileWaiting)
                _planeController?.SetInputEnabled(false);

            // 记录按 F 时刻的 Animator state，用于防止“刚按下时已经在 HoldOpen，于是立刻满足条件”。
            if (usePressStateHashGuard)
            {
                Animator animNow = GetAnimator();
                if (animNow != null)
                {
                    var st = animNow.GetCurrentAnimatorStateInfo(idleOpenStateLayer);
                    _animStateHashAtPress = st.shortNameHash;
                }
            }

            return;
        }

        if (attachViaAnimationEvent)
        {
            // 由 Animation Event 在动画播放完毕时调用真正的 AttachNow。
            _scheduledCandidate = _candidate;
            _pendingAttach = false;
            _attachAt = 0f;
        }
        else
        {
            // 使用旧逻辑：按固定延迟 Attach。
            _scheduledCandidate = null;
            _pendingAttach = true;
            _attachAt = Time.time + Mathf.Max(0f, attachDelay);
        }
    }

    /// <summary>
    /// 给 Animator 的 Animation Event 调用：用于在动画播放完毕后真正抓取箱子。
    /// </summary>
    public void AttachNowFromAnimationEvent()
    {
        _pendingAttach = false;
        AttachNow();
    }

    void AttachNow()
    {
        Grabbable candidateToAttach = _candidate != null ? _candidate : _scheduledCandidate;
        if (candidateToAttach == null || _droneRb == null || grabPoint == null) return;

        _holding = candidateToAttach;
        _candidate = null;
        _scheduledCandidate = null;

        Rigidbody boxRb = _holding.Rigidbody;
        if (boxRb == null) return;

        // 爪子/机械臂可能有碰撞体：即使我们把箱子对齐到 GrabPoint，
        // 碰撞解算也可能立刻把它推回去，让你看起来“没有移动到爪瓣中间”。
        // 抓取期间临时忽略箱子与爪子碰撞，释放时再恢复。
        IgnoreCollisionsBetweenBoxAndGripper(boxRb);

        // 如果箱子 Rigidbody 设了 FreezePosition，MovePosition/FixedJoint 都可能完全不生效。
        // 抓取阶段临时解除平移冻结，释放后再恢复原约束。
        if (!_savedBoxConstraints)
        {
            _prevBoxConstraints = boxRb.constraints;
            _savedBoxConstraints = true;
        }
        boxRb.constraints = _prevBoxConstraints & ~(RigidbodyConstraints.FreezePositionX |
                                                     RigidbodyConstraints.FreezePositionY |
                                                     RigidbodyConstraints.FreezePositionZ);

        // 抓取对齐瞬移后，短时间关闭箱子对环境的碰撞检测，
        // 避免立刻被环境接触解算“挤回原位”，导致你看不到对齐修正效果。
        if (!_savedBoxDetectCollisions)
        {
            _prevBoxDetectCollisions = boxRb.detectCollisions;
            _savedBoxDetectCollisions = true;
        }
        boxRb.detectCollisions = false;
        _restoreCollisionsAtTime = Time.time + Mathf.Max(0f, attachCollisionDisableSeconds);

        // 临时切到 Kinematic：清掉已有接触，避免瞬移后的第一轮解算把箱子立刻挤回原位。
        if (!_savedBoxIsKinematic)
        {
            _prevBoxIsKinematic = boxRb.isKinematic;
            _savedBoxIsKinematic = true;
        }
        boxRb.isKinematic = true;

        if (enableAntiTunneling)
        {
            // 抓住时关闭箱子的重力：否则箱子会拖拽无人机向下，导致你看到的“悬停时下坠/抖动”
            _prevBoxUseGravity = boxRb.useGravity;
            _savedBoxGravity = true;
            boxRb.useGravity = false;
        }

        if (enableAntiTunneling && !_savedRbSettings)
        {
            _prevCcd = boxRb.collisionDetectionMode;
            _prevInterp = boxRb.interpolation;
            _savedRbSettings = true;
        }
        if (enableAntiTunneling)
        {
            boxRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            boxRb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        Vector3 attachWorld = GetGrabPointCenterWorld();

        Vector3 geometryCenterWorld = GetGeometryCenterWorld(boxRb);
        // 强制：把“箱子几何中心”对齐到爪子中心（GrabPoint）。
        // 这样无论从边/底抓，箱子最终都会移到三爪中间。
        Vector3 delta = attachWorld - geometryCenterWorld;
        Vector3 newBoxWorldPos = boxRb.position + delta;

        // 避免一连接就被拉扯产生巨大扭矩：先对齐位置并清速度
        Vector3 boxBefore = boxRb.position;
        float deltaSqr = (newBoxWorldPos - boxRb.position).sqrMagnitude;
        if (deltaSqr > 1e-10f)
        {
            // 直接写 position 更容易在同一物理帧里体现“明显修正位移”
            boxRb.position = newBoxWorldPos;
            Physics.SyncTransforms();
            boxRb.WakeUp();
        }
        else
        {
            // 兜底：很小的修正用 MovePosition 以保持物理一致性
            boxRb.MovePosition(newBoxWorldPos);
            Physics.SyncTransforms();
        }
        Vector3 boxAfterAlign = boxRb.position;
        Vector3 boxAfterKinematicFalse;

        // 恢复为动态刚体，再创建 FixedJoint，进入正常 holding 解算。
        boxRb.isKinematic = false;
        // 关键：切回动态刚体后，Unity 可能会把物理内部状态 snap 回到旧位置。
        // 这里再次写入目标位置，确保后续关节解算从期望的位置开始。
        boxRb.position = newBoxWorldPos;
        Physics.SyncTransforms();
        boxAfterKinematicFalse = boxRb.position;

        boxRb.velocity = Vector3.zero;
        boxRb.angularVelocity = Vector3.zero;

        // 用 FixedJoint 把箱子连到无人机刚体，并将锚点对齐到 GrabPoint（否则会绕无人机质心“吊”起来导致失衡）
        _joint = boxRb.gameObject.AddComponent<FixedJoint>();
        _joint.connectedBody = _droneRb;
        _joint.breakForce = breakForce;
        _joint.breakTorque = breakTorque;
        _joint.enableCollision = false;
        _joint.autoConfigureConnectedAnchor = false;
        // 关键：用“箱子几何中心”作为 FixedJoint 的 anchor，
        // 这样 holding 时约束会驱动物体使几何中心对齐 GrabPoint（爪瓣中间）。
        Vector3 geometryCenterWorldAfter = GetGeometryCenterWorld(boxRb);
        _joint.anchor = boxRb.transform.InverseTransformPoint(geometryCenterWorldAfter);
        _joint.connectedAnchor = _droneRb.transform.InverseTransformPoint(attachWorld);
        _joint.massScale = payloadMassScale;

        if (debugAlignment) _debugFramesRemaining = 5;

        if (effectTransition != null)
            effectTransition.SendMessage("ShowEffect2");

        if (debugAlignment)
        {
            Vector3 jointAnchorWorld = boxRb.transform.TransformPoint(_joint.anchor);
            Vector3 jointConnectedAnchorWorld = _droneRb.transform.TransformPoint(_joint.connectedAnchor);
            float dGeometry = (geometryCenterWorld - attachWorld).magnitude;
            Vector3 newPosGeometry = boxBefore + (attachWorld - geometryCenterWorld);
            Debug.Log(
                $"[DroneGripper] attachWorld={attachWorld} geometryCenter={geometryCenterWorld} " +
                $"delta={delta} deltaMag={delta.magnitude} dGeometryMag={dGeometry} newPosGeometry={newPosGeometry} " +
                $"boxPos before={boxBefore} afterAlign={boxAfterAlign} afterKinematicFalse={boxAfterKinematicFalse} newPos={newBoxWorldPos} " +
                $"jointAnchorWorld={jointAnchorWorld} connectedAnchorWorld={jointConnectedAnchorWorld} " +
                $"boxConstraints(after)={boxRb.constraints} isKinematic={boxRb.isKinematic}");
        }
    }

    void IgnoreCollisionsBetweenBoxAndGripper(Rigidbody boxRb)
    {
        // 避免重复忽略导致 pairs 越积越多
        if (_ignoredCollisionPairs.Count > 0) return;

        var boxCols = boxRb.GetComponentsInChildren<Collider>(includeInactive: true);
        if (boxCols == null || boxCols.Length == 0) return;

        // 抓取期间可能不仅爪子在碰撞：无人机其它机械臂/机身碰撞体也会把箱子挤回原位。
        // 因此忽略 box 与“整个无人机刚体对象及其子节点”的碰撞。
        var droneCols = _droneRb.GetComponentsInChildren<Collider>(includeInactive: true);
        if (droneCols == null || droneCols.Length == 0) return;

        for (int i = 0; i < boxCols.Length; i++)
        {
            var a = boxCols[i];
            if (a == null) continue;
            for (int j = 0; j < droneCols.Length; j++)
            {
                var b = droneCols[j];
                if (b == null) continue;
                if (a == b) continue;

                Physics.IgnoreCollision(a, b, true);
                _ignoredCollisionPairs.Add(new ColliderPair(a, b));
            }
        }
    }

    Vector3 GetClosestPointOnBoxWorld(Rigidbody boxRb, Vector3 worldPoint, out Collider bestCollider)
    {
        bestCollider = null;
        if (boxRb == null) return Vector3.zero;

        // 如果箱子有多个 Collider，选择离 worldPoint 最近的那个碰撞体的 ClosestPoint。
        var cols = boxRb.GetComponentsInChildren<Collider>(includeInactive: true);
        if (cols == null || cols.Length == 0)
            return boxRb.position;

        float bestDistSqr = float.PositiveInfinity;
        Vector3 best = boxRb.position;
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null) continue;
            Vector3 p = c.ClosestPoint(worldPoint);
            float dSqr = (p - worldPoint).sqrMagnitude;
            if (dSqr < bestDistSqr)
            {
                bestDistSqr = dSqr;
                best = p;
                bestCollider = c;
            }
        }

        return best;
    }

    Vector3 GetGrabPointCenterWorld()
    {
        if (grabPoint == null) return Vector3.zero;

        if (useGrabPointTransformPosition)
            return grabPoint.position;

        // 如果爪子/爪瓣没有 Collider（仅有 MeshRenderer），那用 Renderer.bounds.center 会把机械臂的渲染边界也算进去，
        // 导致“中心点”偏移。此时直接使用 GrabPoint Transform 位置（你手动把它放到三爪中间即可）。
        var allColliders = grabPoint.GetComponentsInChildren<Collider>(includeInactive: true);
        if (allColliders == null || allColliders.Length == 0)
            return grabPoint.position;

        // 优先用“抓取根节点下所有渲染/碰撞体”的 bounds.center 计算爪瓣中心，
        // 避免 GrabPoint 自己挂的 Collider（有时会偏到边缘）导致抓取时贴边穿模。
        if (useCollidersBounds)
        {
            var cols = grabPoint.GetComponentsInChildren<Collider>(includeInactive: true);
            if (cols != null && cols.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    if (c == null) continue;
                    sum += c.bounds.center;
                    count++;
                }
                if (count > 0) return sum / count;
            }
        }

        if (useRenderersBounds)
        {
            var renderers = grabPoint.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers != null && renderers.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    sum += r.bounds.center;
                    count++;
                }
                if (count > 0) return sum / count;
            }
        }

        // 兜底：如果 GrabPoint 自己有 Collider，就用它的 bounds.center；
        // 否则取子物体 Collider 平均；最终退回 grabPoint.position。
        var col = grabPoint.GetComponent<Collider>();
        if (col != null) return col.bounds.center;

        var childCols = grabPoint.GetComponentsInChildren<Collider>(includeInactive: true);
        if (childCols != null && childCols.Length > 0)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < childCols.Length; i++)
            {
                var c = childCols[i];
                if (c == null) continue;
                sum += c.bounds.center;
                count++;
            }
            if (count > 0) return sum / count;
        }

        return grabPoint.position;
    }

    Vector3 GetGeometryCenterWorld(Rigidbody rb)
    {
        if (rb == null) return Vector3.zero;

        // 箱子优先用 BoxCollider bounds.center：
        // 原因：箱子往往是“单体体积”，BoxCollider 的 center 通常是最稳定的对齐中心；
        // 使用 renderer 平均值时，若箱子层级包含额外子网格/装饰，会导致中心偏移。
        var box = rb.GetComponent<BoxCollider>();
        if (box != null) return box.bounds.center;

        var boxes = rb.GetComponentsInChildren<BoxCollider>(includeInactive: true);
        if (boxes != null && boxes.Length > 0)
        {
            BoxCollider best = null;
            float bestVol = -1f;
            for (int i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i];
                if (b == null) continue;
                Vector3 size = b.size;
                float vol = size.x * size.y * size.z;
                if (vol > bestVol)
                {
                    bestVol = vol;
                    best = b;
                }
            }
            if (best != null) return best.bounds.center;
        }

        // 可选：再尝试用 collider/renderer 平均值兜底。
        if (useCollidersBounds)
        {
            var cols = rb.GetComponentsInChildren<Collider>(includeInactive: true);
            if (cols != null && cols.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    if (c == null) continue;
                    sum += c.bounds.center;
                    count++;
                }
                if (count > 0) return sum / count;
            }
        }

        if (useRenderersBounds)
        {
            var renderers = rb.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers != null && renderers.Length > 0)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    sum += r.bounds.center;
                    count++;
                }
                if (count > 0) return sum / count;
            }
        }

        // 最后兜底：用当前 Rigidbody 位置
        return rb.position;
    }

    void RecenterGrabPoint()
    {
        if (grabPoint == null) return;

        Vector3 sum = Vector3.zero;
        int count = 0;

        if (useRenderersBounds)
        {
            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var r = renderers[i];
                    if (r == null) continue;
                    sum += r.bounds.center;
                    count++;
                }
            }
        }
        else if (useCollidersBounds)
        {
            var cols = GetComponentsInChildren<Collider>(includeInactive: true);
            if (cols != null)
            {
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i];
                    if (c == null) continue;
                    sum += c.bounds.center;
                    count++;
                }
            }
        }

        if (count <= 0) return;
        grabPoint.position = sum / count;
    }

    void BeginRelease()
    {
        Animator anim = GetAnimator();
        if (anim != null)
        {
            // 优先用 releaseTriggerName；不存在时才尝试 fallback。
            if (!string.IsNullOrEmpty(releaseTriggerName) && HasTriggerParameter(anim, releaseTriggerName))
            {
                anim.SetTrigger(releaseTriggerName);
            }
            else
            {
                // 兼容：Animator 参数可能写成了 Relsase（或其它你填的 fallback）
                if (!string.IsNullOrEmpty(releaseTriggerFallbackName) && releaseTriggerFallbackName != releaseTriggerName)
                    TrySetTrigger(anim, releaseTriggerFallbackName);
            }
        }

        if (_holding == null) return;

        _waitingForIdleOpen = false;
        if (stopPlaneInputWhileWaiting)
            _planeController?.SetInputEnabled(true);

        Rigidbody boxRb = _holding.Rigidbody;
        _holding = null;
        _scheduledCandidate = null;

        if (boxRb != null)
        {
            var j = boxRb.GetComponent<FixedJoint>();
            if (j != null) Destroy(j);

            // 释放时恢复碰撞（忽略应仅存在于 holding 期间）
            for (int i = 0; i < _ignoredCollisionPairs.Count; i++)
            {
                var p = _ignoredCollisionPairs[i];
                if (p.a != null && p.b != null)
                    Physics.IgnoreCollision(p.a, p.b, false);
            }
            _ignoredCollisionPairs.Clear();

            if (enableAntiTunneling)
            {
                // 释放后保持一段时间 CCD，避免高速抛出穿地（tunneling）
                if (_savedRbSettings)
                {
                    // 把“原设置”复制出来，协程结束后再恢复（不要在这里立刻恢复）
                    CollisionDetectionMode restoreCcd = _prevCcd;
                    RigidbodyInterpolation restoreInterp = _prevInterp;
                    _savedRbSettings = false;

                    boxRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    boxRb.interpolation = RigidbodyInterpolation.Interpolate;
                    StartCoroutine(RestoreRbAfterDelay(boxRb, restoreCcd, restoreInterp, postReleaseCcdSeconds));
                }
                else
                {
                    boxRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    boxRb.interpolation = RigidbodyInterpolation.Interpolate;
                }

                if (_savedBoxGravity)
                {
                    boxRb.useGravity = _prevBoxUseGravity;
                    _savedBoxGravity = false;
                }

                // 释放瞬间上抬一点，避免与地面/爪子重叠造成穿模或弹飞
                boxRb.position = boxRb.position + Vector3.up * releaseUpOffset;

                // 限速（可选）：避免速度过大导致 tunneling
                float v = boxRb.velocity.magnitude;
                if (v > releaseMaxSpeed)
                    boxRb.velocity = boxRb.velocity.normalized * releaseMaxSpeed;
            }

            if (_savedBoxConstraints)
            {
                boxRb.constraints = _prevBoxConstraints;
                _savedBoxConstraints = false;
            }

            if (_savedBoxIsKinematic)
            {
                boxRb.isKinematic = _prevBoxIsKinematic;
                _savedBoxIsKinematic = false;
            }

            if (_savedBoxDetectCollisions)
            {
                boxRb.detectCollisions = _prevBoxDetectCollisions;
                _savedBoxDetectCollisions = false;
            }
        }
    }

    IEnumerator RestoreRbAfterDelay(Rigidbody rb, CollisionDetectionMode ccd, RigidbodyInterpolation interp, float seconds)
    {
        if (rb == null) yield break;
        float t = Mathf.Max(0.05f, seconds);
        yield return new WaitForSeconds(t);
        if (rb == null) yield break;
        rb.collisionDetectionMode = ccd;
        rb.interpolation = interp;
    }

    public bool IsHolding => _holding != null;
    public float HoldingMass => _holding != null && _holding.Rigidbody != null ? _holding.Rigidbody.mass : 0f;

    /// <summary>
    /// GrabPoint 的中心点（用于 PlaneController 计算 FixedJoint 相对质心的偏移）。
    /// </summary>
    public Vector3 GrabPointCenterWorld => GetGrabPointCenterWorld();

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!_wasHoldingBeforeSceneLoad) return;
        _wasHoldingBeforeSceneLoad = false;

        var allPackages = FindObjectsOfType<Grabbable>();
        Grabbable target = null;
        foreach (var p in allPackages)
        {
            if (p.name == carriedPackageName || p.name.Contains(carriedPackageName))
            {
                target = p;
                break;
            }
        }

        if (target != null)
        {
            ReAttachPackage(target);
        }
    }

    /// <summary>
    /// 切换场景前调用：保存抓取状态，使无人机跨场景存活，并触发场景切换。
    /// </summary>
    public void PrepareForSceneTransition(string targetScene)
    {
        if (_holding == null) return;

        _wasHoldingBeforeSceneLoad = true;
        _carriedRelativeOffset = _holding.transform.position - transform.position;

        Rigidbody boxRb = _holding.Rigidbody;
        _holding = null;

        if (boxRb != null)
        {
            var j = boxRb.GetComponent<FixedJoint>();
            if (j != null) Destroy(j);
        }
        _joint = null;

        DontDestroyOnLoad(gameObject);

        SceneManager.LoadScene(targetScene);
    }

    void ReAttachPackage(Grabbable package)
    {
        _holding = package;
        Rigidbody boxRb = package.Rigidbody;
        if (boxRb == null) return;

        boxRb.position = transform.position + _carriedRelativeOffset;
        boxRb.velocity = Vector3.zero;
        boxRb.angularVelocity = Vector3.zero;

        _joint = boxRb.gameObject.AddComponent<FixedJoint>();
        _joint.connectedBody = _droneRb;
        _joint.breakForce = breakForce;
        _joint.breakTorque = breakTorque;
        _joint.autoConfigureConnectedAnchor = false;
        _joint.anchor = Vector3.zero;
        _joint.connectedAnchor = _droneRb.transform.InverseTransformPoint(GetGrabPointCenterWorld());
        _joint.massScale = payloadMassScale;
    }
}

