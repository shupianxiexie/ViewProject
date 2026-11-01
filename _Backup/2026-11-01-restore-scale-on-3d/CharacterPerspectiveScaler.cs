using UnityEngine;

/// <summary>
/// 站在挂有 <see cref="PerspectiveScalePlatform"/> 的平台上时，
/// Q 和 E 按该平台的倍数与轴向缩放角色。
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class CharacterPerspectiveScaler : MonoBehaviour
{
    [Header("Platform Detection")]
    [Tooltip("Extra ray distance below the character collider.")]
    [Min(0f)]
    public float extraGroundCheckDistance = 0.5f;

    [Tooltip("Keep the character's feet at the same height after scaling.")]
    public bool keepFeetPosition = true;

    [Header("Input")]
    public KeyCode scaleByXKey = KeyCode.Q;
    public KeyCode scaleByZKey = KeyCode.E;

    private Collider characterCollider;
    private Rigidbody characterRigidbody;
    private PlayerController playerController;
    private Vector3 initialLocalScale;
    private float initialRigidbodyMass = 1f;
    private float initialGameplayWeight = 1f;

    private void Awake()
    {
        characterCollider = GetComponent<Collider>();
        characterRigidbody = GetComponent<Rigidbody>();
        playerController = GetComponent<PlayerController>();
        initialLocalScale = transform.localScale;

        if (characterRigidbody != null)
        {
            initialRigidbodyMass = characterRigidbody.mass;
        }

        if (playerController != null)
        {
            initialGameplayWeight = playerController.weight;
        }
    }

    private void OnValidate()
    {
        extraGroundCheckDistance = Mathf.Max(0f, extraGroundCheckDistance);
    }

    private void Update()
    {
        if (Input.GetKeyDown(scaleByXKey))
        {
            ScaleFromPositiveEdge(useXAxis: true);
        }

        if (Input.GetKeyDown(scaleByZKey))
        {
            ScaleFromPositiveEdge(useXAxis: false);
        }
    }

    private void ScaleFromPositiveEdge(bool useXAxis)
    {
        if (!TryFindCurrentPlatform(out Collider platformCollider))
        {
            return;
        }

        if (!TryGetScalePlatform(platformCollider, out PerspectiveScalePlatform platform))
        {
            return;
        }

        if (!platform.TryGetMultiplier(
                transform.position,
                useXAxis,
                platformCollider.bounds,
                out float multiplier))
        {
            return;
        }

        ApplyScale(multiplier);
    }

    private bool TryFindCurrentPlatform(out Collider platformCollider)
    {
        platformCollider = null;

        Bounds characterBounds = characterCollider != null
            ? characterCollider.bounds
            : new Bounds(transform.position, Vector3.zero);
        Vector3 rayOrigin = characterBounds.center + Vector3.up * 0.05f;
        float rayDistance = characterBounds.extents.y + extraGroundCheckDistance + 0.05f;

        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            rayDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (!IsSupportingSurface(hit.collider) || hit.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = hit.distance;
            platformCollider = hit.collider;
        }

        return platformCollider != null;
    }

    private static bool IsSupportingSurface(Collider candidate)
    {
        return candidate != null &&
               (TryGetScalePlatform(candidate, out _) ||
                candidate.CompareTag("Ground") ||
                candidate.GetComponentInParent<Floor>() != null ||
                candidate.GetComponentInParent<Box>() != null);
    }

    private static bool TryGetScalePlatform(
        Collider candidate,
        out PerspectiveScalePlatform platform)
    {
        platform = null;
        if (candidate == null)
        {
            return false;
        }

        platform = candidate.GetComponentInParent<PerspectiveScalePlatform>();
        if (platform != null)
        {
            return true;
        }

        // 压扁时真正踩到的碰撞体在父级 ViewScaleRoot 上，脚本仍留在原来的平台上。
        if (candidate.GetComponent<ViewScaleRoot>() != null)
        {
            platform = candidate.GetComponentInChildren<PerspectiveScalePlatform>();
        }

        return platform != null;
    }

    private void ApplyScale(float multiplier)
    {
        float previousFeetHeight = characterCollider != null
            ? characterCollider.bounds.min.y
            : transform.position.y;

        transform.localScale = initialLocalScale * multiplier;

        if (characterRigidbody != null)
        {
            characterRigidbody.mass = initialRigidbodyMass * multiplier;
        }

        if (playerController != null)
        {
            playerController.weight = initialGameplayWeight * multiplier;
        }

        if (!keepFeetPosition || characterCollider == null)
        {
            return;
        }

        Physics.SyncTransforms();
        float heightCorrection = previousFeetHeight - characterCollider.bounds.min.y;
        Vector3 correctedPosition = transform.position + Vector3.up * heightCorrection;

        if (characterRigidbody != null)
        {
            characterRigidbody.position = correctedPosition;
        }
        else
        {
            transform.position = correctedPosition;
        }
    }
}
