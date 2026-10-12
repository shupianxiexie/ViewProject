using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class CarryLight : InteractableItem
{
    public static CarryLight HeldLight { get; private set; }

    [Header("Orbit")]
    [Tooltip("Orbit center. Uses the parent (LightCenter) when empty.")]
    public Transform center;

    [Tooltip("Radius on the XZ plane. Height changes do not change this distance.")]
    public float radius = 8f;

    [Tooltip("World angle around the center: 0 = +Z, 90 = +X, 180 = -Z, 270 = -X.")]
    public float minAngle = 90f;
    [Tooltip("World angle around the center: 0 = +Z, 90 = +X, 180 = -Z, 270 = -X.")]
    public float maxAngle = 180f;

    [Tooltip("Degrees along the orbit per unit of Mouse X. Negative inverts.")]
    public float moveSensitivity = 2f;

    [Header("Height")]
    [Tooltip("Height above the orbit center.")]
    public float minHeight = 0f;
    [Tooltip("Height above the orbit center.")]
    public float maxHeight = 8f;

    [Tooltip("Height change per unit of Mouse Y. Negative inverts.")]
    public float heightSensitivity = 0.25f;

    Rigidbody rb;
    float currentAngle;
    float currentHeight;

    public bool IsHeld => HeldLight == this;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        if (center == null)
        {
            center = transform.parent;
        }
    }

    void Start()
    {
        if (center == null)
        {
            return;
        }

        Vector3 offset = transform.position - center.position;
        currentHeight = Mathf.Clamp(offset.y, minHeight, maxHeight);
        currentAngle = ClampAngle(Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
        ApplyPose();
    }

    void Update()
    {
        if (!IsHeld || center == null)
        {
            return;
        }

        currentAngle = ClampAngle(currentAngle - Input.GetAxis("Mouse X") * moveSensitivity);
        currentHeight = Mathf.Clamp(
            currentHeight + Input.GetAxis("Mouse Y") * heightSensitivity,
            minHeight,
            maxHeight
        );
        ApplyPose();
    }

    void ApplyPose()
    {
        float radians = currentAngle * Mathf.Deg2Rad;
        Vector3 orbitOffset = new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * radius;
        transform.position = center.position + orbitOffset + Vector3.up * currentHeight;

        Vector3 toCenter = center.position - transform.position;
        if (toCenter.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);
        }
    }

    float ClampAngle(float angle)
    {
        float wrapped = minAngle + Mathf.Repeat(angle - minAngle, 360f);
        if (wrapped <= maxAngle)
        {
            return wrapped;
        }

        float pastMax = wrapped - maxAngle;
        float beforeMin = minAngle + 360f - wrapped;
        return pastMax < beforeMin ? maxAngle : minAngle;
    }

    public override void Interact()
    {
        if (IsHeld)
        {
            HeldLight = null;
        }
        else if (Box.HeldBox == null && HeldLight == null)
        {
            HeldLight = this;
        }
    }

    void OnDestroy()
    {
        if (HeldLight == this)
        {
            HeldLight = null;
        }
    }
}
