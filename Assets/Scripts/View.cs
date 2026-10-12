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

    void ChangeView()
    {
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Q) && playerController.turnTo2DX)
        {
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
        if (!flattenSystem.TryEnter2D(State.Version2DX))
        {
            return;
        }

        currentState = State.Version2DX;
        SyncCamera();
    }

    void ChangeFrom2DX()
    {
        flattenSystem.RestoreFrom2D(State.Version2DX);
        currentState = State.Version3D;
        SyncCamera();
    }

    void ChangeTo2DZ()
    {
        if (!flattenSystem.TryEnter2D(State.Version2DZ))
        {
            return;
        }

        currentState = State.Version2DZ;
        SyncCamera();
    }

    void ChangeFrom2DZ()
    {
        flattenSystem.RestoreFrom2D(State.Version2DZ);
        currentState = State.Version3D;
        SyncCamera();
    }

    void SyncCamera()
    {
        CameraController cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
        {
            cameraController.ChangeCamera(currentState);
        }
    }
}
