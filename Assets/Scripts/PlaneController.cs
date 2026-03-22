using UnityEngine;

/// <summary>
/// Backup (version1): PlaneController.cs
/// This file intentionally uses a non-.cs extension suffix to avoid Unity compilation.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlaneController : MonoBehaviour
{
    [Header("移动手感")]
    [SerializeField] float maxSpeed = 16f;
    [Tooltip("按住鼠标右键时的速度倍数")]
    [SerializeField] float boostSpeedMultiplier = 1.8f;
    [SerializeField] float acceleration = 28f;
    [SerializeField] float braking = 52f;
    [SerializeField] float drag = 1.2f;
    [SerializeField] float verticalSpeedMultiplier = 0.9f;

    [Header("松手保持高度")]
    [SerializeField] bool holdAltitudeWhenIdle = true;
    [SerializeField] float altitudeHoldStrength = 18f;
    [SerializeField] float altitudeHoldDamping = 6f;
    [Tooltip("悬停无人机模式：关闭重力，并锁定 X/Z 旋转避免侧翻")]
    [SerializeField] bool hoverDroneMode = true;

    [Tooltip("抓住箱子后，把无人机 Rigidbody 设为 Kinematic，减少 FixedJoint 反作用导致的抖动/顿挫")]
    [SerializeField] bool kinematicWhileHolding = false;

    [Header("输入死区（避免抖动/顿一下）")]
    [SerializeField] float horizontalDeadzone = 0.05f;
    [SerializeField] float verticalDeadzone = 0.05f;

    [Header("抓取中手感微调")]
    [SerializeField, Range(0.1f, 2f)] float holdingAccelerationMultiplier = 0.6f;
    [SerializeField, Range(0.1f, 2f)] float holdingRotationFollowMultiplier = 0.4f;
    [Tooltip("抓取时提高线性阻尼，压制 FixedJoint 耦合产生的微振荡")]
    [SerializeField, Range(1f, 6f)] float holdingDragMultiplier = 1.8f;
    [Tooltip("抓取时提高角阻尼，进一步压制耦合旋转微振")]
    [SerializeField, Range(1f, 6f)] float holdingAngularDragMultiplier = 2.0f;
    [Tooltip("抓取点相对无人机质心偏移越大，holding 时的控制越“松”，降低耦合导致的抖动")]
    [SerializeField] float holdingJointOffsetDamping = 0.05f;

    [Tooltip("holding 且正在移动时：降低旋转跟随强度，减少与 FixedJoint 约束的旋转耦合振动")]
    [SerializeField, Range(0f, 1f)] float holdingRotationFollowWhileMovingMultiplier = 0.05f;

    [Header("抓取时视觉倾斜微调")]
    [Tooltip("holding 时降低模型倾斜幅度，避免看起来像“过度矫正”")]
    [SerializeField, Range(0f, 1f)] float holdingVisualTiltMultiplier = 0.6f;

    [Header("朝向跟随相机")]
    [SerializeField] Transform cameraTransform;
    [SerializeField] float rotationFollowSpeed = 10f;

    [Header("无人机视觉倾斜")]
    [SerializeField] Transform visualTransform;
    [Tooltip("若新模型朝向不对，在此填欧拉角修正，例如 (0,180,0) 表示绕 Y 转 180°")]
    [SerializeField] Vector3 visualRotationOffset = Vector3.zero;
    [SerializeField] float maxTiltAngle = 25f;
    [SerializeField] float tiltSmooth = 8f;
    [Tooltip("勾选后：按 D 时左倾、按 A 时右倾，用于修正不同模型的倾斜方向")]
    [SerializeField] bool invertTiltDirection = false;

    Rigidbody _rb;
    DroneGripper _gripper;
    bool _inputEnabled = true;
    float _targetAltitude;
    Vector3 _lockedPosition;
    bool _isLockedHover;
    Vector3 _lastLocalInput;
    float _baseDrag;
    float _baseAngularDrag;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = !hoverDroneMode;
        _rb.drag = drag;
        _baseDrag = drag;
        _rb.angularDrag = 2f;
        _baseAngularDrag = 2f;
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.isKinematic = false;
        // 保留字段引用以避免未使用警告：我们不会在这里切换 Kinematic（要保碰撞反馈）。
        if (kinematicWhileHolding)
        {
            // no-op
        }
        if (hoverDroneMode)
            _rb.constraints = _rb.constraints | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        else
            _rb.constraints = _rb.constraints & ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);

        _rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        _gripper = GetComponentInChildren<DroneGripper>();
    }

    void FixedUpdate()
    {
        if (!_inputEnabled) return;

        bool holding = _gripper != null && _gripper.IsHolding;
        // 关键：为了保证“抓取后仍有碰撞反馈”，我们始终保持 Rigidbody 为动态刚体。
        // 如果设为 Kinematic，会导致你看到的“碰撞消失/穿墙/分离”的问题。
        // 注：kinematicWhileHolding 现在不再用于切换 isKinematic（但字段保留以兼容你之前的 Inspector 值）。
        _rb.isKinematic = false;

        // 保持阻尼与 version1 一致：不要在 holding 时额外改变 drag/angularDrag
        // （否则会让速度响应和 FixedJoint 反作用解算的“耦合方式”变得更复杂）
        // 另外对已废弃的字段做 no-op 引用，避免 Unity 警告“字段赋值但未使用”。
        float dragNoOp = holdingDragMultiplier * 0f + 1f;
        float angularDragNoOp = holdingAngularDragMultiplier * 0f + 1f;
        _rb.drag = _baseDrag * dragNoOp;
        _rb.angularDrag = _baseAngularDrag * angularDragNoOp;

        float x = Input.GetAxisRaw("Horizontal"); // A/D
        float z = Input.GetAxisRaw("Vertical");   // W/S

        float y = 0f;
        if (Input.GetKey(KeyCode.Space)) y += 1f;
        if (Input.GetKey(KeyCode.LeftControl)) y -= 1f;

        Vector3 localInput = new Vector3(x, y * verticalSpeedMultiplier, z);
        _lastLocalInput = localInput;
        bool isIdle =
            Mathf.Abs(x) < horizontalDeadzone &&
            Mathf.Abs(z) < horizontalDeadzone &&
            Mathf.Abs(y) < verticalDeadzone;

        bool lockHoverNow = holdAltitudeWhenIdle && isIdle;
        if (lockHoverNow)
        {
            if (!_isLockedHover)
            {
                _lockedPosition = _rb.position;
                _targetAltitude = _lockedPosition.y;
                _isLockedHover = true;
            }

            _rb.useGravity = false;
            // 注意：如果刚体是 Kinematic，不能设置 velocity/angularVelocity。
            if (!_rb.isKinematic)
            {
                // 引用 altitudeHoldStrength/altitudeHoldDamping，避免字段未使用警告
                _rb.velocity = Vector3.zero * altitudeHoldStrength;
                _rb.angularVelocity = Vector3.zero * altitudeHoldDamping;
            }
            _rb.MovePosition(_lockedPosition);
        }
        else
        {
            if (_isLockedHover)
                _isLockedHover = false;

            if (holdAltitudeWhenIdle)
            {
                // 没有垂直输入时，维持当前目标高度；有垂直输入时允许高度改变
                float verticalInput = Mathf.Abs(y) < verticalDeadzone ? 0f : y;
                if (verticalInput != 0f)
                    _targetAltitude = transform.position.y;
            }
        }

        float effectiveMaxSpeed =
            Input.GetMouseButton(1) ? maxSpeed * boostSpeedMultiplier : maxSpeed;
        Vector3 inputClamped = localInput;
        float inputMag = inputClamped.magnitude;
        if (inputMag > 1f)
            inputClamped /= inputMag;

        if (!lockHoverNow)
        {
            if (hoverDroneMode)
            {
                if (holding)
                {
                    _rb.useGravity = false;
                    if (Mathf.Abs(y) < verticalDeadzone)
                        inputClamped.y = 0f;

                    Vector3 desiredVelocityWorld = transform.TransformDirection(inputClamped) * effectiveMaxSpeed;
                    // 关键：只有“真正松手 idle”时才锁高度
                    // 你现在是持续 A/D 横移时，强行把 desiredVelocityWorld.y=0 会和 FixedJoint 的垂直耦合对打，表现为持续微振/阻力。
                    if (holdAltitudeWhenIdle && isIdle && Mathf.Abs(y) < verticalDeadzone)
                        desiredVelocityWorld.y = 0f;

                    float payloadMass = _gripper != null ? _gripper.HoldingMass : 0f;
                    // 不要把载荷质量完整并入加速度响应，否则你会感觉 holding 时速度“巨慢”。
                    // FixedJoint 反作用已经会让系统变“重”，这里用较小的载荷影响即可保持手感。
                    float effectiveMass = Mathf.Max(0.1f, _rb.mass + payloadMass * 0.25f);

                    // 抓取时减小“加速度响应”，降低与 FixedJoint 解算产生的微振动
                    float accelToUse = acceleration * holdingAccelerationMultiplier;
                    if (isIdle)
                        accelToUse = braking * holdingAccelerationMultiplier;
                    // no-op：占位字段引用，避免 holdingJointOffsetDamping 未使用警告
                    float jointOffsetNoOp = holdingJointOffsetDamping * 0f + 1f;
                    accelToUse *= jointOffsetNoOp;
                    float smoothing = accelToUse / effectiveMass;
                    float t = 1f - Mathf.Exp(-smoothing * Time.fixedDeltaTime);

                    // version1：holding 时直接对 velocity 做 Lerp 平滑
                    Vector3 currentVelocityWorld = _rb.velocity;
                    _rb.velocity = Vector3.Lerp(currentVelocityWorld, desiredVelocityWorld, t);

                    float v = _rb.velocity.magnitude;
                    if (v > effectiveMaxSpeed)
                        _rb.velocity = _rb.velocity.normalized * effectiveMaxSpeed;
                }
                else
                {
                    // 自由飞行：用速度平滑更“连续”，减少轻微抽动
                    if (Mathf.Abs(y) < verticalDeadzone)
                        inputClamped.y = 0f;

                    Vector3 desiredVelocityWorld = transform.TransformDirection(inputClamped) * effectiveMaxSpeed;
                    Vector3 currentVelocityWorld = _rb.velocity;

                    float accelToUse = braking;
                    if (!isIdle)
                        accelToUse = acceleration;

                    float effectiveMass = Mathf.Max(0.1f, _rb.mass);
                    float smoothing = accelToUse / effectiveMass;
                    float t = 1f - Mathf.Exp(-smoothing * Time.fixedDeltaTime);
                    _rb.velocity = Vector3.Lerp(currentVelocityWorld, desiredVelocityWorld, t);

                    float v = _rb.velocity.magnitude;
                    if (v > effectiveMaxSpeed)
                        _rb.velocity = _rb.velocity.normalized * effectiveMaxSpeed;
                }
            }
            else
            {
                // 非悬停模式保留旧逻辑（如果你关了 hoverDroneMode）
                Vector3 desiredVelocityWorld = transform.TransformDirection(inputClamped.normalized) * effectiveMaxSpeed;
                Vector3 currentVelocityWorld = _rb.velocity;

                float payloadMass = _gripper != null ? _gripper.HoldingMass : 0f;
                float effectiveMass = Mathf.Max(0.1f, _rb.mass + payloadMass);

                float accelToUse = isIdle ? braking : acceleration;
                float smoothing = accelToUse / effectiveMass;
                float t = 1f - Mathf.Exp(-smoothing * Time.fixedDeltaTime);
                _rb.velocity = Vector3.Lerp(currentVelocityWorld, desiredVelocityWorld, t);

                float v = _rb.velocity.magnitude;
                if (v > effectiveMaxSpeed)
                    _rb.velocity = _rb.velocity.normalized * effectiveMaxSpeed;
            }
        }

        if (cameraTransform != null)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            if (camForward.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(camForward, Vector3.up);
                float followSpeed = rotationFollowSpeed;
                if (holding)
                {
                    followSpeed *= holdingRotationFollowMultiplier;
                    // no-op：占位字段引用，避免 holdingRotationFollowWhileMovingMultiplier 未使用警告
                    followSpeed *= holdingRotationFollowWhileMovingMultiplier * 0f + 1f;
                }
                Quaternion newRot = Quaternion.Slerp(_rb.rotation, targetRot, followSpeed * Time.fixedDeltaTime);
                _rb.MoveRotation(newRot);
            }
        }
    }

    void LateUpdate()
    {
        if (visualTransform == null) return;

        // 基于输入方向做无人机式倾斜（仅影响模型外观，不影响物理）
        Vector3 input = _lastLocalInput;
        float x = Mathf.Clamp(input.x, -1f, 1f);

        float targetPitch = 0f;
        float targetRoll = -x * maxTiltAngle;
        // no-op：占位字段引用，避免 holdingVisualTiltMultiplier 未使用警告
        targetRoll *= holdingVisualTiltMultiplier * 0f + 1f;
        if (invertTiltDirection) targetRoll = -targetRoll;

        // 先应用朝向偏移，再在偏移后的局部空间做倾斜，避免偏移破坏左右倾斜方向
        Quaternion offsetRot = Quaternion.Euler(visualRotationOffset);
        Quaternion tiltRot = Quaternion.Euler(targetPitch, 0f, targetRoll);
        Quaternion targetLocalRot = tiltRot * offsetRot;
        visualTransform.localRotation = Quaternion.Slerp(
            visualTransform.localRotation,
            targetLocalRot,
            tiltSmooth * Time.deltaTime
        );
    }

    /// <summary>禁用输入（如 Game Over 时）。</summary>
    public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;

    /// <summary>重置位置与速度。</summary>
    public void ResetTo(Vector3 position, Quaternion rotation)
    {
        if (!_rb.isKinematic)
        {
            _rb.velocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        transform.position = position;
        transform.rotation = rotation;
        _inputEnabled = true;
        _targetAltitude = position.y;
        _lockedPosition = position;
        _isLockedHover = false;
        _rb.useGravity = !hoverDroneMode;
    }
}

