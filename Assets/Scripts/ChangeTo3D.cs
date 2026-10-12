using UnityEngine;

public class ChangeTo3D : MonoBehaviour
{
    public void ChangeFrom2DX()
    {
        SnapPlayerFrom2D(View.State.Version2DX);
    }

    public void ChangeFrom2DZ()
    {
        SnapPlayerFrom2D(View.State.Version2DZ);
    }

    public void SnapPlayerFrom2D(View.State fromState)
    {
        Vector3 playerPosition = transform.position;
        if (fromState == View.State.Version2DX)
        {
            playerPosition.x = FindFloorAxisFrom2DX(playerPosition);
        }
        else if (fromState == View.State.Version2DZ)
        {
            playerPosition.z = FindFloorAxisFrom2DZ(playerPosition);
        }

        ApplyWorldPosition(transform, playerPosition);
    }

    public static float FindFloorAxisFrom2DX(Vector3 worldPosition)
    {
        float axis = worldPosition.x;
        foreach (Floor floor in Floor.allFloors)
        {
            if (floor == null || !floor.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = ViewWorldBounds.Get(floor.transform);
            if (worldPosition.z >= bounds.min.z && worldPosition.z <= bounds.max.z)
            {
                axis = bounds.center.x;
            }
        }

        foreach (ShadowPlatform platform in Object.FindObjectsOfType<ShadowPlatform>())
        {
            if (platform == null ||
                !platform.gameObject.activeInHierarchy ||
                !platform.MatchesView(View.State.Version2DX))
            {
                continue;
            }

            Bounds bounds = ViewWorldBounds.Get(platform.transform);
            if (worldPosition.z >= bounds.min.z && worldPosition.z <= bounds.max.z)
            {
                axis = bounds.center.x;
            }
        }

        return axis;
    }

    public static float FindFloorAxisFrom2DZ(Vector3 worldPosition)
    {
        float axis = worldPosition.z;
        foreach (Floor floor in Floor.allFloors)
        {
            if (floor == null || !floor.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = ViewWorldBounds.Get(floor.transform);
            if (worldPosition.x >= bounds.min.x && worldPosition.x <= bounds.max.x)
            {
                axis = bounds.center.z;
            }
        }

        foreach (ShadowPlatform platform in Object.FindObjectsOfType<ShadowPlatform>())
        {
            if (platform == null ||
                !platform.gameObject.activeInHierarchy ||
                !platform.MatchesView(View.State.Version2DZ))
            {
                continue;
            }

            Bounds bounds = ViewWorldBounds.Get(platform.transform);
            if (worldPosition.x >= bounds.min.x && worldPosition.x <= bounds.max.x)
            {
                axis = bounds.center.z;
            }
        }

        return axis;
    }

    static void ApplyWorldPosition(Transform target, Vector3 worldPosition)
    {
        Rigidbody body = target.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = worldPosition;
        }

        target.position = worldPosition;
    }
}
