using UnityEngine;

public class GroundChecker : MonoBehaviour
{
    public bool isGrounded = true;
    public float groundCheckDistance = 0.35f;

    PlayerController playerController;

    void Awake()
    {
        playerController = GetComponentInParent<PlayerController>();
    }

    void OnTriggerStay(Collider other)
    {
        if (CanRegisterGround() && IsGroundCollider(other))
        {
            isGrounded = true;
        }
    }

    void FixedUpdate()
    {
        if (CanRegisterGround() && IsSupportedByGround())
        {
            isGrounded = true;
        }
        else if (!CanRegisterGround())
        {
            isGrounded = false;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsGroundCollider(other) && !IsSupportedByGround())
        {
            isGrounded = false;
        }
    }

    bool IsSupportedByGround()
    {
        Transform probe = playerController != null ? playerController.transform : transform;
        Bounds bounds = ViewWorldBounds.Get(probe);
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + 0.08f, bounds.center.z);
        float distance = Mathf.Max(0.12f, groundCheckDistance);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider != null &&
                hitCollider.transform != probe &&
                !hitCollider.transform.IsChildOf(probe) &&
                IsGroundCollider(hitCollider))
            {
                return true;
            }
        }

        return false;
    }

    bool CanRegisterGround()
    {
        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        if (playerController != null && !playerController.CanRegisterGround)
        {
            return false;
        }

        Rigidbody body = playerController != null ? playerController.GetComponent<Rigidbody>() : null;
        if (body != null && body.velocity.y > 0.4f)
        {
            return false;
        }

        return true;
    }

    bool IsGroundCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        return other.CompareTag("Ground") ||
               other.GetComponentInParent<Box>() != null ||
               other.GetComponentInParent<ShadowPlatform>() != null ||
               other.GetComponentInParent<Floor>() != null;
    }
}
