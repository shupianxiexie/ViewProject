using UnityEngine;

public class View : MonoBehaviour
{
    public enum State
    {
        Version3D,
        Version2DX,
        Version2DZ,
    }

    public State currentState = State.Version3D;

    [Header("2D Flatten")]
    [Tooltip("Projected overlap area / target area required to hide an object or block view change.")]
    [Range(0f, 1f)]
    public float occlusionOverlapRatio = 0.1f;

    [Tooltip("Keep flattened objects this far inside the 2D camera near/far clip.")]
    [Min(0f)]
    public float cameraDepthPadding = 0.5f;

    [Tooltip("Thickness along the camera depth after flattening onto the 2D plane.")]
    [Min(0.05f)]
    public float flattenThickness = 0.25f;

    ViewFlattenSystem flattenSystem;
    bool cameraTransitionArmed;
    View.State pendingState;

    void Awake()
    {
        flattenSystem = new ViewFlattenSystem(this);
    }

    void Start()
    {
        ShadowConeEmitter.EnsureInitialized(this);
    }

    void Update()
    {
        ChangeView();
    }

    void LateUpdate()
    {
        flattenSystem.SyncPlayerVisual();
    }

    void ChangeView()
    {
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController == null || !playerController.inputEnabled)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q) && playerController.turnTo2DX)
        {
            // 镜头还在飞时不接受新的切换。压扁外形跟着这次飞行走。
            if (IsCameraTransitioning())
            {
                return;
            }

            switch (currentState)
            {
                case State.Version3D:
                    ChangeTo2DX();
                    break;
                case State.Version2DX:
                    ChangeFrom2DX();
                    break;
            }
        }

        if (Input.GetKeyDown(KeyCode.E) && playerController.turnTo2DZ)
        {
            if (IsCameraTransitioning())
            {
                return;
            }

            switch (currentState)
            {
                case State.Version3D:
                    ChangeTo2DZ();
                    break;
                case State.Version2DZ:
                    ChangeFrom2DZ();
                    break;
            }
        }
    }

    void ChangeTo2DX()
    {
        BeginEnter2D(State.Version2DX);
    }

    void ChangeFrom2DX()
    {
        BeginExit2D(State.Version2DX);
    }

    void ChangeTo2DZ()
    {
        BeginEnter2D(State.Version2DZ);
    }

    void ChangeFrom2DZ()
    {
        BeginExit2D(State.Version2DZ);
    }

    // 通过重叠检查后才起飞。视角状态先留在 3D，等镜头到了再改成 2D。
    void BeginEnter2D(State state)
    {
        if (!flattenSystem.PrepareEnter(state))
        {
            return;
        }

        pendingState = state;
        cameraTransitionArmed = true;
        SyncCamera(state);
    }

    // 先把角色放到恢复后要站的地面上，再让镜头起飞。
    // 角色和地面的厚度仍跟着镜头一起从扁变回立体。
    void BeginExit2D(State fromState)
    {
        flattenSystem.PrepareExit(fromState);
        pendingState = State.Version3D;
        cameraTransitionArmed = true;
        SyncCamera(State.Version3D);
    }

    // 和镜头用同一个 0 到 1。0 是飞行起点的外形，1 是飞完后的外形。
    public void SetFlattenAlongCamera(float t)
    {
        if (!cameraTransitionArmed)
        {
            return;
        }

        flattenSystem.SetVisualProgress(t);
    }

    // 镜头到位时调用。这时才换碰撞、处理遮挡，并切到目标操作。
    public void OnCameraArrived()
    {
        if (!cameraTransitionArmed)
        {
            return;
        }

        flattenSystem.CommitActiveTransition();
        currentState = pendingState;
        cameraTransitionArmed = false;
    }

    // 过渡途中的 Q/E 直接丢掉，不排队等当前这条飞完。
    bool IsCameraTransitioning()
    {
        CameraController cameraController = FindObjectOfType<CameraController>();
        return cameraController != null && cameraController.IsTransitioning;
    }

    void SyncCamera(State state)
    {
        CameraController cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
        {
            cameraController.ChangeCamera(state);
            return;
        }

        // 没有相机管理器时不能等飞行结束，否则缩放父节点会一直留着。
        OnCameraArrived();
    }
}
