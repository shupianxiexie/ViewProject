using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 镜头进入 2D 时，若角色正在 <see cref="PerspectiveScalePlatform"/> 的触发区域内，
/// 按当时的位置定一次倍数，这次 2D 里不再变。
/// 2DX 用 X：靠近正方向变大。2DZ 用 Z：远离正方向变大。
/// 回到 3D 后恢复初始大小。
/// </summary>
[DisallowMultipleComponent]
public class CharacterPerspectiveScaler : MonoBehaviour
{
    [Header("Feet")]
    [Tooltip("Keep the character's feet at the same height after scaling.")]
    public bool keepFeetPosition = true;

    readonly List<PerspectiveScalePlatform> activeZones = new List<PerspectiveScalePlatform>();
    bool fixedForCurrentView;

    Collider characterCollider;
    Rigidbody characterRigidbody;
    PlayerController playerController;
    Vector3 initialLocalScale;
    float initialRigidbodyMass = 1f;
    float initialGameplayWeight = 1f;

    public float CurrentMultiplier { get; private set; } = 1f;

    public Vector3 InitialLocalScale => initialLocalScale;

    void Awake()
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

    public void EnterScaleZone(PerspectiveScalePlatform zone)
    {
        if (zone == null || activeZones.Contains(zone))
        {
            return;
        }

        activeZones.Add(zone);
    }

    public void ExitScaleZone(PerspectiveScalePlatform zone)
    {
        activeZones.Remove(zone);
    }

    // 只在切入 2D 的那一刻取一次。人不在区域内就不改大小。这次 2D 里之后都保持。
    public bool TryApplyForView(View.State state)
    {
        if (state == View.State.Version3D)
        {
            return false;
        }

        if (fixedForCurrentView)
        {
            return CurrentMultiplier > 0f;
        }

        fixedForCurrentView = true;
        Physics.SyncTransforms();

        PerspectiveScalePlatform zone = ZoneAtViewChange();
        if (zone == null)
        {
            return CurrentMultiplier > 0f;
        }

        bool useXAxis = state == View.State.Version2DX;
        if (!zone.AllowsAxis(useXAxis) ||
            !zone.TryGetMultiplier(transform.position, useXAxis, out float multiplier))
        {
            return CurrentMultiplier > 0f;
        }

        bool unchanged = Mathf.Abs(multiplier - CurrentMultiplier) <= 0.0001f;
        CurrentMultiplier = multiplier;
        if (!unchanged)
        {
            ApplyScale(multiplier);
        }

        return true;
    }

    // 镜头回到 3D 并拆掉压扁结构之后调用。恢复进游戏时的身体大小、质量和重量。
    public void RestoreOriginalSize()
    {
        fixedForCurrentView = false;
        CurrentMultiplier = 1f;
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

    PerspectiveScalePlatform ZoneAtViewChange()
    {
        PerspectiveScalePlatform current = CurrentZone();
        if (current != null && current.OverlapsCharacter(this))
        {
            return current;
        }

        for (int i = activeZones.Count - 1; i >= 0; i--)
        {
            PerspectiveScalePlatform zone = activeZones[i];
            if (zone != null && zone.OverlapsCharacter(this))
            {
                return zone;
            }
        }

        return PerspectiveScalePlatform.FindOverlapping(this);
    }

    PerspectiveScalePlatform CurrentZone()
    {
        for (int i = activeZones.Count - 1; i >= 0; i--)
        {
            if (activeZones[i] == null)
            {
                activeZones.RemoveAt(i);
                continue;
            }

            return activeZones[i];
        }

        return null;
    }

    void ApplyScale(float multiplier)
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
        float targetFootY = previousFootY;
        bool liftOutOfGround = false;
        if (TryGetGroundSurfaceY(previousFootY, out float surfaceY))
        {
            float skin = Mathf.Max(0f, characterCollider.contactOffset);
            if (previousFootY < surfaceY + skin)
            {
                targetFootY = surfaceY + skin;
                liftOutOfGround = true;
            }
        }

        float newFootY = transform.position.y + GetFootOffset(transform);
        if (Mathf.Abs(newFootY - targetFootY) <= 0.001f)
        {
            return;
        }

        PlaceFeetAt(targetFootY, liftOutOfGround);
    }

    // 胶囊体底端贴到原来的脚底高度。陷进地面时抬到表面之上。
    void PlaceFeetAt(float surfaceY, bool stopVerticalVelocity)
    {
        float footOffset = GetFootOffset(transform);
        Vector3 position = transform.position;
        position.y = surfaceY - footOffset;

        if (characterRigidbody != null)
        {
            characterRigidbody.position = position;
            if (stopVerticalVelocity)
            {
                Vector3 velocity = characterRigidbody.velocity;
                velocity.y = 0f;
                characterRigidbody.velocity = velocity;
            }
        }

        transform.position = position;
        Physics.SyncTransforms();
    }

    bool TryGetGroundSurfaceY(float footY, out float surfaceY)
    {
        surfaceY = 0f;
        if (characterCollider == null)
        {
            return false;
        }

        float originY = characterCollider.bounds.max.y + 1f;
        Vector3 origin = new Vector3(transform.position.x, originY, transform.position.z);
        float rayDistance = Mathf.Max(1f, originY - footY + 5f);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            rayDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float nearestGap = float.PositiveInfinity;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            Transform hitTransform = hit.collider.transform;
            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                continue;
            }

            // 只认脚附近的地面，避免打到头顶的平台。
            if (hit.normal.y < 0.45f || hit.point.y > footY + 0.75f)
            {
                continue;
            }

            float gap = Mathf.Abs(hit.point.y - footY);
            if (gap >= nearestGap)
            {
                continue;
            }

            nearestGap = gap;
            surfaceY = hit.point.y;
            found = true;
        }

        return found;
    }

    // 与 ViewFlattenSystem 贴地时使用的胶囊体底端一致。
    static float GetFootOffset(Transform player)
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
