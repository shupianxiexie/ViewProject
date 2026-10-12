using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
[DefaultExecutionOrder(100)]
public class Box : InteractableItem
{
    public static Box HeldBox { get; private set; }

    public float weight = 1f;
    public float dropForwardForce = 2f;
    public float dropUpwardForce = 0.5f;
    public float dropTorque = 1.5f;

    [Header("3D Hold Position")]
    public float holdForwardDistance = 1.2f;
    public float holdHeight = 1.0f;

    [Header("Portal-like Physics")]
    public float drag = 0.2f;
    public float angularDrag = 0.05f;

    private Rigidbody rb;
    private Collider boxCollider;
    private Transform player;
    private PlayerController playerController;
    private View view;
    private Collider playerCollider;
    private Quaternion rotationBeforeHeld;

    public bool IsHeld => HeldBox == this;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        boxCollider = GetComponent<Collider>();
        ConfigureRigidbody();
        ApplyPhysicsMaterial();

        if (!CompareTag("Ground"))
        {
            tag = "Ground";
        }

        ShadowConeEmitter.EnsureBoxCaster(this);
    }

    void ConfigureRigidbody()
    {
        rb.mass = weight;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.None;
        rb.drag = drag;
        rb.angularDrag = angularDrag;
        rb.centerOfMass = Vector3.zero;
    }

    void ApplyPhysicsMaterial()
    {
        if (boxCollider == null)
        {
            return;
        }

        PhysicMaterial boxMaterial = new PhysicMaterial("BoxPhysics")
        {
            dynamicFriction = 0.5f,
            staticFriction = 0.5f,
            bounciness = 0.05f,
            frictionCombine = PhysicMaterialCombine.Average,
            bounceCombine = PhysicMaterialCombine.Minimum
        };

        boxCollider.material = boxMaterial;
    }

    void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            return;
        }

        player = playerObject.transform;
        playerController = playerObject.GetComponent<PlayerController>();
        view = playerObject.GetComponent<View>();
        playerCollider = playerObject.GetComponent<Collider>();
    }

    void LateUpdate()
    {
        if (!IsHeld)
        {
            return;
        }

        UpdateHeldPosition();
    }

    void UpdateHeldPosition()
    {
        if (player == null)
        {
            return;
        }

        if (view != null && view.currentState == View.State.Version3D &&
            playerController != null)
        {
            Vector3 facingDirection = playerController.FacingDirection;
            if (facingDirection.sqrMagnitude < 0.001f)
            {
                facingDirection = player.forward;
                facingDirection.y = 0f;
                facingDirection.Normalize();
            }

            Vector3 holdPosition =
                player.position +
                facingDirection * holdForwardDistance +
                Vector3.up * holdHeight;

            SetHeldWorldPose(holdPosition, Quaternion.identity);
            return;
        }

        if (view != null && view.currentState == View.State.Version2DX)
        {
            SetHeldWorldPose(
                new Vector3(
                    player.position.x,
                    player.position.y + holdHeight,
                    player.position.z + holdForwardDistance),
                Quaternion.identity);
            return;
        }

        if (view != null && view.currentState == View.State.Version2DZ)
        {
            SetHeldWorldPose(
                new Vector3(
                    player.position.x + holdForwardDistance,
                    player.position.y + holdHeight,
                    player.position.z),
                Quaternion.identity);
        }
    }

    void SetHeldWorldPose(Vector3 holdPosition, Quaternion holdRotation)
    {
        Transform root = ViewScaleRoot.GetRootOrSelf(transform);
        root.position = holdPosition;
        transform.rotation = holdRotation;
    }

    public override void Interact()
    {
        if (!IsHeld)
        {
            PickUp();
        }
        else
        {
            Drop();
        }
    }

    void PickUp()
    {
        if (player == null)
        {
            return;
        }

        rotationBeforeHeld = transform.rotation;
        HeldBox = this;

        ViewScaleRoot.GetRootOrSelf(transform).SetParent(null);

        rb.isKinematic = true;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        boxCollider.enabled = false;

        if (playerCollider != null)
        {
            Physics.IgnoreCollision(boxCollider, playerCollider, true);
        }

        UpdateHeldPosition();
    }

    void Drop()
    {
        if (player == null)
        {
            return;
        }

        Vector3 releaseDirection = GetDropDirection();
        Vector3 releasePosition = transform.position;
        Quaternion releaseRotation = rotationBeforeHeld;

        HeldBox = null;

        Transform root = ViewScaleRoot.GetRootOrSelf(transform);
        root.SetParent(null);
        transform.position = releasePosition;
        transform.rotation = releaseRotation;

        boxCollider.enabled = true;

        if (playerCollider != null)
        {
            StartCoroutine(ReenablePlayerCollision(playerCollider));
        }

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = GetDroppedConstraints();
        rb.velocity = releaseDirection * dropForwardForce + Vector3.up * dropUpwardForce;
        rb.angularVelocity = Vector3.zero;

        if (releaseDirection.sqrMagnitude > 0.001f)
        {
            Vector3 torqueAxis = Vector3.Cross(Vector3.up, releaseDirection.normalized);
            rb.AddTorque(torqueAxis * dropTorque, ForceMode.Impulse);
        }

        Physics.SyncTransforms();
    }

    RigidbodyConstraints GetDroppedConstraints()
    {
        if (view == null)
        {
            return RigidbodyConstraints.None;
        }

        if (view.currentState == View.State.Version2DX)
        {
            return RigidbodyConstraints.FreezePositionX |
                   RigidbodyConstraints.FreezeRotationX |
                   RigidbodyConstraints.FreezeRotationY |
                   RigidbodyConstraints.FreezeRotationZ;
        }

        if (view.currentState == View.State.Version2DZ)
        {
            return RigidbodyConstraints.FreezePositionZ |
                   RigidbodyConstraints.FreezeRotationX |
                   RigidbodyConstraints.FreezeRotationY |
                   RigidbodyConstraints.FreezeRotationZ;
        }

        return RigidbodyConstraints.None;
    }

    IEnumerator ReenablePlayerCollision(Collider targetPlayerCollider)
    {
        yield return new WaitForSeconds(0.3f);

        if (targetPlayerCollider != null && boxCollider != null)
        {
            Physics.IgnoreCollision(boxCollider, targetPlayerCollider, false);
        }
    }

    Vector3 GetDropDirection()
    {
        if (view == null || playerController == null)
        {
            Vector3 forward = player.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        }

        if (view.currentState == View.State.Version3D &&
            playerController != null)
        {
            Vector3 forward = playerController.FacingDirection;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.001f)
            {
                return forward.normalized;
            }
        }
        else if (view.currentState == View.State.Version2DX)
        {
            return Vector3.forward;
        }
        else if (view.currentState == View.State.Version2DZ)
        {
            return Vector3.right;
        }

        Vector3 playerForward = player.forward;
        playerForward.y = 0f;
        return playerForward.sqrMagnitude > 0.001f ? playerForward.normalized : Vector3.forward;
    }

    void OnDestroy()
    {
        if (HeldBox == this)
        {
            HeldBox = null;
        }

        if (playerCollider != null && boxCollider != null)
        {
            Physics.IgnoreCollision(boxCollider, playerCollider, false);
        }
    }
}
