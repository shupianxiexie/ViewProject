using UnityEngine;

// 3D、2DX、2DZ 的切换。
// Q/E 按下后不立刻硬切：当前这台相机沿二阶贝塞尔飞到目标机位，
// 飞行过程中始终看向玩家，并把透视和正交的投影矩阵按同一个进度混在一起。
// 飞完才启用目标相机。过渡没结束时再次按 Q/E，由 View 直接丢掉。
public class CameraController : MonoBehaviour
{
    public Camera3D camera3D;
    public Camera2DX camera2DX;
    public Camera2DZ camera2DZ;
    public PlayerController playerController;

    public Transform player;

    [Header("镜头过渡")]
    [Tooltip("3D 和 2D 切换时，镜头走完贝塞尔曲线的时间（秒）。")]
    [Min(0f)]
    public float transitionDuration = 0.8f;

    [Tooltip("只改变沿曲线的快慢，不改变曲线形状。横轴是时间比例，纵轴是曲线参数。")]
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("控制点偏离中点的比例，按起点到终点的距离计算。0 是直线，越大弧线越往侧面鼓，用来绕开玩家。")]
    [Range(0f, 0.5f)]
    public float curveBend = 0.15f;

    public View.State currentState = View.State.Version3D;

    // 给 View 用。为 true 时 Q/E 不再改视角，也不再重开一条过渡。
    public bool IsTransitioning { get; private set; }

    View playerView;
    Rigidbody playerRigidbody;

    View.State transitionFrom;
    View.State transitionTo;
    float transitionElapsed;
    Vector3 transitionStartPosition;
    Camera movingCamera;

    void Awake()
    {
        if (transitionCurve == null || transitionCurve.length == 0)
        {
            transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (player != null)
        {
            playerView = player.GetComponent<View>();
            playerRigidbody = player.GetComponent<Rigidbody>();
        }
    }

    void Start()
    {
        // 开场没有上一台相机，直接摆到 3D，不播过渡。
        ChangeCamera(View.State.Version3D, true);
    }

    void LateUpdate()
    {
        if (IsTransitioning)
        {
            // 三台相机的跟拍也写在 LateUpdate。过渡放在这里，并且已经把跟拍停掉，
            // 这样本帧写入的曲线位置不会被跟拍盖掉。
            AdvanceTransition();
            return;
        }

        if (playerView == null && player != null)
        {
            playerView = player.GetComponent<View>();
        }

        if (playerView != null && playerView.currentState != currentState)
        {
            ChangeCamera(playerView.currentState);
        }
    }

    public void ChangeCamera(View.State state)
    {
        ChangeCamera(state, false);
    }

    public void ChangeCamera(View.State state, bool immediate)
    {
        // View 已经挡过一次。这里再挡一次，避免别的调用把正在飞的镜头打断。
        if (!immediate && IsTransitioning)
        {
            return;
        }

        if (!immediate && state != currentState && transitionDuration > 0f)
        {
            BeginTransition(state);
            return;
        }

        ApplyImmediate(state);
    }

    void BeginTransition(View.State state)
    {
        movingCamera = GetUnityCamera(currentState);
        if (movingCamera == null)
        {
            ApplyImmediate(state);
            return;
        }

        transitionFrom = currentState;
        transitionTo = state;
        // 起点锁在按下切换这一刻。终点每帧按玩家当前位置重算，避免人走开后镜头落点错位。
        transitionStartPosition = movingCamera.transform.position;
        transitionElapsed = 0f;
        currentState = state;
        IsTransitioning = true;
        SetSuspendFollow(true);
        // 约束先不动。2D 操作、碰撞和遮挡都等镜头飞到，在 ApplyImmediate 里一起提交。
    }

    void AdvanceTransition()
    {
        if (movingCamera == null)
        {
            ApplyImmediate(transitionTo);
            return;
        }

        transitionElapsed += Time.deltaTime;
        float rawT = transitionDuration > 0f
            ? Mathf.Clamp01(transitionElapsed / transitionDuration)
            : 1f;

        if (rawT >= 1f)
        {
            CompleteTransition();
            return;
        }

        // 缓动曲线只重映射时间。位置仍然采样下面的二阶贝塞尔，形状不会变成这条 AnimationCurve。
        // 压扁用同一个 curveT，物体厚度和深度跟镜头一起走。
        float curveT = EvaluateCurve(rawT);
        if (playerView != null)
        {
            playerView.SetFlattenAlongCamera(curveT);
        }

        Vector3 endPosition = GetDesiredPosition(transitionTo);
        Vector3 controlPoint = GetControlPoint(transitionStartPosition, endPosition);

        movingCamera.transform.position = QuadraticBezier(
            transitionStartPosition,
            controlPoint,
            endPosition,
            curveT
        );
        LookAtPlayer(movingCamera.transform);
        ApplyBlendedProjection(curveT);
    }

    void CompleteTransition()
    {
        IsTransitioning = false;
        // 先放开跟拍再启用目标相机，目标相机下一帧才能自己跟上玩家。
        SetSuspendFollow(false);
        ApplyImmediate(transitionTo);
    }

    void ApplyImmediate(View.State state)
    {
        if (playerView == null && player != null)
        {
            playerView = player.GetComponent<View>();
        }

        // 开场摆相机时没有压扁过渡，OnCameraArrived 会自己空过。
        // 真正切换时，这里才把外形收到终点，并换上碰撞和操作。
        if (playerView != null)
        {
            playerView.OnCameraArrived();
        }

        currentState = state;
        IsTransitioning = false;
        SetSuspendFollow(false);
        ApplyConstraints(state);
        ResetAllCameraMatrices();

        camera3D.gameObject.SetActive(false);
        camera2DX.gameObject.SetActive(false);
        camera2DZ.gameObject.SetActive(false);

        switch (state)
        {
            case View.State.Version3D:
                camera3D.gameObject.SetActive(true);
                break;
            case View.State.Version2DX:
                camera2DX.gameObject.SetActive(true);
                break;
            case View.State.Version2DZ:
                camera2DZ.gameObject.SetActive(true);
                break;
        }

        Camera targetCamera = GetUnityCamera(state);
        if (targetCamera == null)
        {
            return;
        }

        // 2D 相机的 OnEnable 会把旋转扳回轴向，并打开正交。
        // 位置要写在那之后，这一帧才不会停在切换前的坐标上。
        // 3D 没有这段 OnEnable，这里补上透视，清掉过渡时写过的自定义投影。
        if (state == View.State.Version3D)
        {
            targetCamera.orthographic = false;
        }

        targetCamera.ResetProjectionMatrix();
        targetCamera.ResetCullingMatrix();
        targetCamera.transform.position = GetDesiredPosition(state);

        if (state == View.State.Version3D)
        {
            LookAtPlayer(targetCamera.transform);
        }
    }

    void ApplyConstraints(View.State state)
    {
        switch (state)
        {
            case View.State.Version3D:
                playerRigidbody.constraints =
                    RigidbodyConstraints.FreezeRotationX |
                    RigidbodyConstraints.FreezeRotationY |
                    RigidbodyConstraints.FreezeRotationZ;
                break;

            case View.State.Version2DX:
                playerRigidbody.constraints =
                    RigidbodyConstraints.FreezePositionX |
                    RigidbodyConstraints.FreezeRotationX |
                    RigidbodyConstraints.FreezeRotationY |
                    RigidbodyConstraints.FreezeRotationZ;
                break;

            case View.State.Version2DZ:
                playerRigidbody.constraints =
                    RigidbodyConstraints.FreezePositionZ |
                    RigidbodyConstraints.FreezeRotationX |
                    RigidbodyConstraints.FreezeRotationY |
                    RigidbodyConstraints.FreezeRotationZ;
                break;
        }
    }

    // 二阶贝塞尔：B(t) = (1-t)^2 P0 + 2(1-t)t P1 + t^2 P2。
    // P0 是起点，P2 是终点，P1 是控制点，曲线本身不经过 P1，只朝它鼓出去。
    static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    Vector3 GetControlPoint(Vector3 start, Vector3 end)
    {
        Vector3 mid = (start + end) * 0.5f;
        Vector3 chord = end - start;
        float chordLength = chord.magnitude;
        if (chordLength < 0.001f || curveBend <= 0f)
        {
            return mid;
        }

        // 侧向是弦在水平面上的垂线。弦几乎竖直时，改用世界右方向再求一次。
        Vector3 side = Vector3.Cross(chord, Vector3.up);
        if (side.sqrMagnitude < 0.0001f)
        {
            side = Vector3.Cross(chord, Vector3.right);
        }

        if (side.sqrMagnitude < 0.0001f)
        {
            return mid;
        }

        side.Normalize();

        // 往远离玩家的那一侧偏。直线若正好穿过角色，镜头会从旁边绕过去。
        if (player != null && Vector3.Dot(side, player.position - mid) > 0f)
        {
            side = -side;
        }

        return mid + side * (chordLength * curveBend);
    }

    void LookAtPlayer(Transform cameraTransform)
    {
        if (player == null || cameraTransform == null)
        {
            return;
        }

        Vector3 toPlayer = player.position - cameraTransform.position;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            return;
        }

        // 相机几乎在玩家正上方时，用世界上方向当参考会和视线平行，旋转会失效。
        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(toPlayer.normalized, Vector3.up)) > 0.99f)
        {
            up = Vector3.forward;
        }

        cameraTransform.rotation = Quaternion.LookRotation(toPlayer, up);
    }

    void ApplyBlendedProjection(float t)
    {
        // 先分别采样两台相机自己的投影，再按元素混合。
        // 采样会临时改正交、视野和裁剪面，所以必须在赋回混合结果之前做完。
        Matrix4x4 from = SampleProjection(transitionFrom);
        Matrix4x4 to = SampleProjection(transitionTo);

        // 从 2D 飞出时，这台相机的正交开关是开着的。
        // 开着的话，引擎可能在渲染前按正交尺寸重算矩阵，把混合结果盖掉。
        // 先关掉，再写入混合矩阵，这一帧才会真的用混合投影。
        movingCamera.orthographic = false;
        movingCamera.projectionMatrix = LerpMatrix(from, to, t);

        // 自定义投影不会带动裁剪矩阵。不写的话，物体可能在飞到一半时被裁掉。
        movingCamera.cullingMatrix =
            movingCamera.projectionMatrix * movingCamera.worldToCameraMatrix;
    }

    Matrix4x4 SampleProjection(View.State state)
    {
        bool orthographic;
        float orthoSize;
        float fov;
        float near;
        float far;
        GetProjectionSettings(
            state,
            out orthographic,
            out orthoSize,
            out fov,
            out near,
            out far
        );

        bool oldOrtho = movingCamera.orthographic;
        float oldSize = movingCamera.orthographicSize;
        float oldFov = movingCamera.fieldOfView;
        float oldNear = movingCamera.nearClipPlane;
        float oldFar = movingCamera.farClipPlane;

        try
        {
            movingCamera.nearClipPlane = near;
            movingCamera.farClipPlane = far;
            movingCamera.fieldOfView = fov;
            movingCamera.orthographicSize = orthoSize;
            movingCamera.orthographic = orthographic;
            // 过渡中这台相机挂着上一帧的自定义矩阵，不重置的话读不到上面这组参数。
            movingCamera.ResetProjectionMatrix();
            return movingCamera.projectionMatrix;
        }
        finally
        {
            movingCamera.orthographic = oldOrtho;
            movingCamera.orthographicSize = oldSize;
            movingCamera.fieldOfView = oldFov;
            movingCamera.nearClipPlane = oldNear;
            movingCamera.farClipPlane = oldFar;
            movingCamera.ResetProjectionMatrix();
        }
    }

    void GetProjectionSettings(
        View.State state,
        out bool orthographic,
        out float orthoSize,
        out float fov,
        out float near,
        out float far)
    {
        Camera cam = GetUnityCamera(state);
        if (cam == null)
        {
            orthographic = false;
            orthoSize = 5f;
            fov = 60f;
            near = 0.3f;
            far = 1000f;
            return;
        }

        near = cam.nearClipPlane;
        far = cam.farClipPlane;
        fov = cam.fieldOfView;
        orthoSize = cam.orthographicSize;

        // 场景里的 2D 相机在运行前仍是透视，正交是 OnEnable 才打开的。
        // 尺寸以脚本上的 orthographicSize 为准，不能读相机组件里还没改过的值。
        if (state == View.State.Version2DX)
        {
            orthographic = true;
            orthoSize = camera2DX.orthographicSize;
        }
        else if (state == View.State.Version2DZ)
        {
            orthographic = true;
            orthoSize = camera2DZ.orthographicSize;
        }
        else
        {
            orthographic = false;
        }
    }

    // 矩阵按 16 个元素做线性混合，用来试透视和正交的交接。
    // 这不是严格的视锥插值，画面缩放如果别扭，再调过渡时长或改混合方式。
    static Matrix4x4 LerpMatrix(Matrix4x4 from, Matrix4x4 to, float t)
    {
        Matrix4x4 result = new Matrix4x4();
        for (int i = 0; i < 16; i++)
        {
            result[i] = Mathf.Lerp(from[i], to[i], t);
        }

        return result;
    }

    float EvaluateCurve(float t)
    {
        if (transitionCurve == null || transitionCurve.length == 0)
        {
            return t;
        }

        return Mathf.Clamp01(transitionCurve.Evaluate(t));
    }

    Vector3 GetDesiredPosition(View.State state)
    {
        switch (state)
        {
            case View.State.Version3D:
                return camera3D.GetOrbitPosition();
            case View.State.Version2DX:
                return camera2DX.GetFollowPosition();
            case View.State.Version2DZ:
                return camera2DZ.GetFollowPosition();
            default:
                return transitionStartPosition;
        }
    }

    Camera GetUnityCamera(View.State state)
    {
        MonoBehaviour behaviour = GetCameraBehaviour(state);
        if (behaviour == null)
        {
            return null;
        }

        return behaviour.GetComponent<Camera>();
    }

    MonoBehaviour GetCameraBehaviour(View.State state)
    {
        switch (state)
        {
            case View.State.Version3D:
                return camera3D;
            case View.State.Version2DX:
                return camera2DX;
            case View.State.Version2DZ:
                return camera2DZ;
            default:
                return null;
        }
    }

    void SetSuspendFollow(bool suspend)
    {
        if (camera3D != null)
        {
            camera3D.suspendFollow = suspend;
        }

        if (camera2DX != null)
        {
            camera2DX.suspendFollow = suspend;
        }

        if (camera2DZ != null)
        {
            camera2DZ.suspendFollow = suspend;
        }
    }

    void ResetAllCameraMatrices()
    {
        ResetCameraMatrices(GetUnityCamera(View.State.Version3D));
        ResetCameraMatrices(GetUnityCamera(View.State.Version2DX));
        ResetCameraMatrices(GetUnityCamera(View.State.Version2DZ));
    }

    static void ResetCameraMatrices(Camera cam)
    {
        if (cam == null)
        {
            return;
        }

        cam.ResetProjectionMatrix();
        cam.ResetCullingMatrix();
    }
}
