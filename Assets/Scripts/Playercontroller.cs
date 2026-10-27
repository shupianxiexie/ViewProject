using UnityEngine;

[DefaultExecutionOrder(50)]
public class PlayerController : MonoBehaviour
{
    public bool turnTo2DX = false;
    public bool turnTo2DZ = false;


    public float weight = 1f;
    public float speed = 10.0f; 
    public float turnSpeed = 360f;

    Rigidbody rb;

    public float jumpForce = 10.0f;

    [Tooltip("Single-tap jump uses this many stacked impulses to match the old hold-to-jump height.")]
    [Min(1f)]
    public float jumpImpulseMultiplier = 2.5f;

    [Tooltip("Ignore ground briefly after jumping so a second tap cannot jump.")]
    [Min(0f)]
    public float jumpGroundLockDuration = 0.2f;

    GroundChecker gc;

    public View view;

    // 开场菜单和镜头飞到角色背后时关掉。落地后由菜单脚本重新打开。
    public bool inputEnabled = true;

    CameraController cameraController;

    public Vector3 FacingDirection { get; private set; } = Vector3.forward;

    private Animator animator;
    private bool isWalk = false;
    private bool jumpRequested;
    float jumpGroundLockUntil;

    public bool CanRegisterGround => Time.time >= jumpGroundLockUntil;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        gc = GetComponentInChildren<GroundChecker>();

        view = GetComponent<View>();

        cameraController = FindObjectOfType<CameraController>();

        animator = GetComponentInChildren<Animator>();
    }


    void Update()
    {
        if (!inputEnabled)
        {
            jumpRequested = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space) &&
            CarryLight.HeldLight == null &&
            gc != null &&
            gc.isGrounded &&
            CanRegisterGround)
        {
            jumpRequested = true;
        }
    }

    void FixedUpdate()
    {
        rb.angularVelocity = Vector3.zero;
        Move();
        ApplyJump();
    }

    void Move()
    {
        Vector2 direction = new Vector2(0, 0);

        if (!inputEnabled)
        {
            Vector3 velocity = rb.velocity;
            velocity.x = 0f;
            velocity.z = 0f;
            rb.velocity = velocity;
            SetWalking(false);
            return;
        }

        if (CarryLight.HeldLight != null)
        {
            Vector3 velocity = rb.velocity;
            velocity.x = 0f;
            velocity.z = 0f;
            rb.velocity = velocity;
            SetWalking(false);
            return;
        }

        if (view.currentState == View.State.Version3D)
        {
            if (Input.GetKey(KeyCode.W))
            {
                direction.y += 1;
            }

            if (Input.GetKey(KeyCode.S))
            {
                direction.y -= 1;
            }

            if (Input.GetKey(KeyCode.A))
            {
                direction.x -= 1;
            }

            if (Input.GetKey(KeyCode.D))
            {
                direction.x += 1;
            }


            if (direction.magnitude > 0)
            {
                Vector3 forward =
                    cameraController.camera3D.transform.forward;

                forward.y = 0;
                forward = forward.normalized;


                Vector3 right =
                    cameraController.camera3D.transform.right;

                right.y = 0;
                right = right.normalized;


                Vector3 moveDirection =
                    forward * direction.y +
                    right * direction.x;

                Vector3 newPosition =
                    rb.position +
                    moveDirection.normalized * speed * Time.deltaTime;

                rb.MovePosition(newPosition);
                FaceMoveDirection(moveDirection);
            }
        }

        if (view.currentState == View.State.Version2DX && turnTo2DX)
        {
            if (Input.GetKey(KeyCode.A))
            {
                direction.x -= 1;
            }

            if (Input.GetKey(KeyCode.D))
            {
                direction.x += 1;
            }

            if (direction.x != 0)
            {
                Vector3 velocity = rb.velocity;
                velocity.z = direction.x * speed;
                rb.velocity = velocity;
                FaceMoveDirection(new Vector3(0f, 0f, direction.x));
            }
            else
            {
                Vector3 velocity = rb.velocity;
                velocity.z = 0f;
                rb.velocity = velocity;
            }
        }

        if (view.currentState == View.State.Version2DZ && turnTo2DZ)
        {
            if (Input.GetKey(KeyCode.A))
            {
                direction.x -= 1;
            }

            if (Input.GetKey(KeyCode.D))
            {
                direction.x += 1;
            }

            if (direction.x != 0)
            {
                Vector3 velocity = rb.velocity;
                velocity.x = direction.x * speed;
                rb.velocity = velocity;
                FaceMoveDirection(new Vector3(direction.x, 0f, 0f));
            }
            else
            {
                Vector3 velocity = rb.velocity;
                velocity.x = 0f;
                rb.velocity = velocity;
            }
        }


        SetWalking(direction.sqrMagnitude > 0.001f);
    }

    void SetWalking(bool walking)
    {
        if (isWalk != walking)
        {
            isWalk = walking;
            if (animator != null)
            {
                animator.SetBool("IsWalk", isWalk);
            }
        }
    }

    void FaceMoveDirection(Vector3 moveDirection)
    {
        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude < 0.001f)
        {
            return;
        }
        moveDirection.Normalize();
        FacingDirection = moveDirection;
        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);

        Quaternion newRotation = Quaternion.RotateTowards(
            rb.rotation,
            targetRotation,
            turnSpeed * Time.fixedDeltaTime
        );

        rb.MoveRotation(newRotation);
    }

    void ApplyJump()
    {
        if (!jumpRequested || gc == null)
        {
            return;
        }

        jumpRequested = false;
        if (!gc.isGrounded)
        {
            return;
        }

        Vector3 velocity = rb.velocity;
        velocity.y = 0f;
        rb.velocity = velocity;
        rb.AddForce(Vector3.up * jumpForce * jumpImpulseMultiplier, ForceMode.Impulse);
        gc.isGrounded = false;
        jumpGroundLockUntil = Time.time + jumpGroundLockDuration;
    }
}