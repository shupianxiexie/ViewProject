using UnityEngine;

/// <summary>
/// 站在挂有 <see cref="PerspectiveScalePlatform"/> 的平台上时，
/// Q 和 E 按该平台的倍数与轴向缩放角色。回到 3D 时恢复进游戏时的大小。
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

        ApplyScale(multiplier, platformCollider);
    }

    // 镜头回到 3D 并拆掉压扁结构之后调用。恢复进游戏时的身体大小、质量和重量。
    public void RestoreOriginalSize()
    {
        if (initialLocalScale == Vector3.zero)
        {
            return;
        }

        transform.localScale = initialLocalScale;

        if (characterRigidbody != null)
        {
            characterRigidbody.mass = initialRigidbodyMass;
        }

        if (playerController != null)
        {
            playerController.weight = initialGameplayWeight;
        }

        Physics.SyncTransforms();
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

    private void ApplyScale(float multiplier, Collider platformCollider)
    {
        float previousFootY = transform.position.y + GetFootOffset(transform);
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
        float skin = 0f;
        if (TryGetSurfaceY(platformCollider, out float surfaceY))
        {
            skin = Mathf.Max(0f, characterCollider.contactOffset);
        }
        else
        {
            surfaceY = previousFootY;
        }

        PlaceFeetAt(surfaceY, skin);
    }

    // 胶囊体底端贴到表面。命中地板时留出接触间距，避免比例变大后陷进去。
    private void PlaceFeetAt(float surfaceY, float skin)
    {
        float footOffset = GetFootOffset(transform);
        Vector3 position = transform.position;
        position.y = surfaceY + skin - footOffset;

        if (characterRigidbody != null)
        {
            characterRigidbody.position = position;
            Vector3 velocity = characterRigidbody.velocity;
            velocity.y = 0f;
            characterRigidbody.velocity = velocity;
        }

        transform.position = position;
        Physics.SyncTransforms();
    }

    private bool TryGetSurfaceY(Collider platformCollider, out float surfaceY)
    {
        surfaceY = 0f;
        if (platformCollider == null)
        {
            return false;
        }

        float originY = Mathf.Max(
            characterCollider.bounds.max.y,
            platformCollider.bounds.max.y) + 1f;
        Vector3 origin = new Vector3(transform.position.x, originY, transform.position.z);
        float rayDistance = originY - platformCollider.bounds.min.y + 1f;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            rayDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.normal.y < 0.45f ||
                hit.distance >= nearestDistance ||
                !IsSamePlatform(hit.collider, platformCollider))
            {
                continue;
            }

            nearestDistance = hit.distance;
            surfaceY = hit.point.y;
            found = true;
        }

        return found;
    }

    private static bool IsSamePlatform(Collider hit, Collider platform)
    {
        if (hit == null || platform == null || hit.transform == null || platform.transform == null)
        {
            return false;
        }

        if (hit.transform == platform.transform ||
            hit.transform.IsChildOf(platform.transform) ||
            platform.transform.IsChildOf(hit.transform))
        {
            return true;
        }

        PerspectiveScalePlatform hitPlatform = hit.GetComponentInParent<PerspectiveScalePlatform>();
        PerspectiveScalePlatform standingPlatform =
            platform.GetComponentInParent<PerspectiveScalePlatform>();
        return hitPlatform != null && hitPlatform == standingPlatform;
    }

    // 与 ViewFlattenSystem 贴地时使用的胶囊体底端一致。
    private static float GetFootOffset(Transform player)
    {
        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            return 0f;
        }

        Vector3 localBottom = capsule.center;
        float halfHeight = capsule.height * 0.5f;
        if (capsule.direction == 0)
        {
            localBottom.x -= halfHeight;
        }
        else if (capsule.direction == 2)
        {
            localBottom.z -= halfHeight;
        }
        else
        {
            localBottom.y -= halfHeight;
        }

        return player.TransformPoint(localBottom).y - player.position.y;
    }
}
