using UnityEngine;

/// <summary>
/// Remembers the platform the player is standing on, and the one they left before that.
/// Platforms are objects with Floor, Box, or ShadowPlatform.
/// </summary>
[DisallowMultipleComponent]
public class PreviousPlatformTracker : MonoBehaviour
{
    Transform currentPlatform;
    Transform previousPlatform;
    bool isGrounded;
    float teleportLockedUntil;

    public bool CanTeleport => Time.time >= teleportLockedUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureOnPlayer()
    {
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null && player.GetComponent<PreviousPlatformTracker>() == null)
        {
            player.gameObject.AddComponent<PreviousPlatformTracker>();
        }
    }

    void FixedUpdate()
    {
        Transform found = FindGroundPlatform();
        isGrounded = found != null;
        if (found == null || found == currentPlatform)
        {
            return;
        }

        if (currentPlatform != null)
        {
            previousPlatform = currentPlatform;
        }

        currentPlatform = found;
    }

    public Transform GetReturnPlatform()
    {
        if (isGrounded)
        {
            return previousPlatform;
        }

        return currentPlatform;
    }

    public void MarkTeleported(Transform arrivedPlatform)
    {
        teleportLockedUntil = Time.time + 0.5f;
        if (arrivedPlatform == null)
        {
            return;
        }

        if (currentPlatform != null && currentPlatform != arrivedPlatform)
        {
            previousPlatform = currentPlatform;
        }

        currentPlatform = arrivedPlatform;
        isGrounded = true;
    }

    Transform FindGroundPlatform()
    {
        Collider bodyCollider = GetComponent<Collider>();
        Bounds bounds = bodyCollider != null
            ? bodyCollider.bounds
            : new Bounds(transform.position, Vector3.zero);
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.08f, bounds.center.z);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            0.45f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        Transform nearestPlatform = null;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null ||
                hitCollider.transform == transform ||
                hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            Transform platform = ResolvePlatform(hitCollider);
            if (platform == null || hits[i].distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = hits[i].distance;
            nearestPlatform = platform;
        }

        return nearestPlatform;
    }

    public static Transform ResolvePlatform(Collider collider)
    {
        if (collider == null)
        {
            return null;
        }

        Floor floor = collider.GetComponentInParent<Floor>();
        if (floor != null)
        {
            return floor.transform;
        }

        Box box = collider.GetComponentInParent<Box>();
        if (box != null)
        {
            return box.transform;
        }

        ShadowPlatform shadowPlatform = collider.GetComponentInParent<ShadowPlatform>();
        if (shadowPlatform != null)
        {
            return shadowPlatform.transform;
        }

        return null;
    }
}
