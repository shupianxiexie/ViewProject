using UnityEngine;

/// <summary>
/// Place this on an object that already has a collider.
/// When the player touches it, the player is moved to the center of the platform they just left.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ReturnToPreviousPlatform : MonoBehaviour
{
    [Tooltip("Extra space kept between the player's feet and the platform surface.")]
    [Min(0f)]
    public float surfaceClearance = 0.05f;

    void Start()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null && player.GetComponent<PreviousPlatformTracker>() == null)
        {
            player.gameObject.AddComponent<PreviousPlatformTracker>();
        }
    }

    void OnTriggerEnter(Collider other)
    {
        TryTeleport(other);
    }

    void OnCollisionEnter(Collision collision)
    {
        TryTeleport(collision.collider);
    }

    void TryTeleport(Collider other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        PreviousPlatformTracker tracker = player.GetComponent<PreviousPlatformTracker>();
        if (tracker == null)
        {
            tracker = player.gameObject.AddComponent<PreviousPlatformTracker>();
        }

        if (!tracker.CanTeleport)
        {
            return;
        }

        Transform platform = tracker.GetReturnPlatform();
        if (platform == null || !platform.gameObject.activeInHierarchy)
        {
            return;
        }

        Vector3 destination = GetDestination(player, platform);
        tracker.MarkTeleported(platform);

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = destination;
        }

        player.transform.position = destination;
        Physics.SyncTransforms();
    }

    Vector3 GetDestination(PlayerController player, Transform platform)
    {
        float surfaceY = GetSurfaceHeight(platform);
        Collider playerCollider = player.GetComponent<Collider>();
        float feetOffset = playerCollider != null
            ? player.transform.position.y - playerCollider.bounds.min.y
            : 1f;

        Bounds bounds = ViewWorldBounds.Get(platform);
        return new Vector3(
            bounds.center.x,
            surfaceY + feetOffset + surfaceClearance,
            bounds.center.z);
    }

    static float GetSurfaceHeight(Transform platform)
    {
        Bounds bounds = ViewWorldBounds.Get(platform);
        Vector3 origin = new Vector3(bounds.center.x, bounds.max.y + 0.5f, bounds.center.z);
        float distance = bounds.size.y + 1.5f;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        float surfaceY = bounds.max.y;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || hits[i].distance >= nearestDistance)
            {
                continue;
            }

            if (PreviousPlatformTracker.ResolvePlatform(hits[i].collider) != platform)
            {
                continue;
            }

            nearestDistance = hits[i].distance;
            surfaceY = hits[i].point.y;
        }

        return surfaceY;
    }
}
