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
        return FindFloorAxis(worldPosition, depthIsX: true, View.State.Version2DX);
    }

    public static float FindFloorAxisFrom2DZ(Vector3 worldPosition)
    {
        return FindFloorAxis(worldPosition, depthIsX: false, View.State.Version2DZ);
    }

    // 同一条走位轴上有多块平台时，用离脚最近的顶面，不用列表里的最后一块。
    static float FindFloorAxis(Vector3 worldPosition, bool depthIsX, View.State state)
    {
        float axis = depthIsX ? worldPosition.x : worldPosition.z;
        bool found = false;
        float bestDistance = float.MaxValue;
        bool haveFallback = false;
        float fallbackDistance = float.MaxValue;
        float fallbackAxis = axis;

        foreach (Floor floor in Floor.allFloors)
        {
            if (floor == null || !floor.gameObject.activeInHierarchy)
            {
                continue;
            }

            ConsiderFloorAxis(
                ViewWorldBounds.Get(floor.transform),
                worldPosition,
                depthIsX,
                ref axis,
                ref found,
                ref bestDistance,
                ref haveFallback,
                ref fallbackDistance,
                ref fallbackAxis);
        }

        foreach (ShadowPlatform platform in Object.FindObjectsOfType<ShadowPlatform>())
        {
            if (platform == null ||
                !platform.gameObject.activeInHierarchy ||
                !platform.MatchesView(state))
            {
                continue;
            }

            ConsiderFloorAxis(
                ViewWorldBounds.Get(platform.transform),
                worldPosition,
                depthIsX,
                ref axis,
                ref found,
                ref bestDistance,
                ref haveFallback,
                ref fallbackDistance,
                ref fallbackAxis);
        }

        if (!found && haveFallback)
        {
            return fallbackAxis;
        }

        return axis;
    }

    static void ConsiderFloorAxis(
        Bounds bounds,
        Vector3 worldPosition,
        bool depthIsX,
        ref float axis,
        ref bool found,
        ref float bestDistance,
        ref bool haveFallback,
        ref float fallbackDistance,
        ref float fallbackAxis)
    {
        bool inside = depthIsX
            ? worldPosition.z >= bounds.min.z && worldPosition.z <= bounds.max.z
            : worldPosition.x >= bounds.min.x && worldPosition.x <= bounds.max.x;
        if (!inside)
        {
            return;
        }

        float top = bounds.max.y;
        float candidate = depthIsX ? bounds.center.x : bounds.center.z;
        float distance = Mathf.Abs(worldPosition.y - top);
        if (!haveFallback || distance < fallbackDistance)
        {
            haveFallback = true;
            fallbackDistance = distance;
            fallbackAxis = candidate;
        }

        if (top > worldPosition.y + 0.35f)
        {
            return;
        }

        if (found && distance >= bestDistance)
        {
            return;
        }

        found = true;
        bestDistance = distance;
        axis = candidate;
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
