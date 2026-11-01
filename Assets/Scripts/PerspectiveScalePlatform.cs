using UnityEngine;

/// <summary>
/// 挂到平台上后，角色在 2D 里会按整块平台的位置缩放。
/// X：靠近正方向变大，远离变小。Z：远离正方向变大，靠近变小。
/// 脚本放在碰撞体物体上，或放在它的父物体上。
/// </summary>
[DisallowMultipleComponent]
public class PerspectiveScalePlatform : MonoBehaviour
{
    [Header("缩放倍数")]
    [Tooltip("这一侧的较小倍数。X 的负方向边缘，或 Z 的正方向边缘。")]
    [Min(0.01f)]
    public float minScaleMultiplier = 0.5f;

    [Tooltip("这一侧的较大倍数。X 的正方向边缘，或 Z 的负方向边缘。")]
    [Min(0.01f)]
    public float maxScaleMultiplier = 2f;

    [Header("允许的轴")]
    [Tooltip("进入 2DZ 后，靠近平台 X 正方向变大，远离变小。")]
    public bool allowScaleByX = true;

    [Tooltip("进入 2DX 后，远离平台 Z 正方向变大，靠近变小。")]
    public bool allowScaleByZ = true;

    private void OnValidate()
    {
        minScaleMultiplier = Mathf.Max(0.01f, minScaleMultiplier);
        maxScaleMultiplier = Mathf.Max(minScaleMultiplier, maxScaleMultiplier);
    }

    /// <summary>
    /// 按整块平台在该轴上的范围计算倍数。
    /// X 靠近正方向变大；Z 远离正方向变大。
    /// </summary>
    public bool TryGetMultiplier(
        Vector3 worldPosition,
        bool useXAxis,
        Bounds fallbackBounds,
        out float multiplier)
    {
        multiplier = 1f;

        if (useXAxis)
        {
            if (!allowScaleByX)
            {
                return false;
            }
        }
        else if (!allowScaleByZ)
        {
            return false;
        }

        if (!TryGetAxisRange(useXAxis, fallbackBounds, out float minimum, out float maximum))
        {
            return false;
        }

        float axisLength = maximum - minimum;
        if (axisLength <= Mathf.Epsilon)
        {
            return false;
        }

        Vector3 axis = useXAxis ? transform.right : transform.forward;
        axis.y = 0f;
        if (axis.sqrMagnitude <= Mathf.Epsilon)
        {
            axis = useXAxis ? Vector3.right : Vector3.forward;
        }

        axis.Normalize();
        float coordinate = Vector3.Dot(worldPosition, axis);
        float distanceRatio = Mathf.Clamp01((maximum - coordinate) / axisLength);
        float minimumMultiplier = Mathf.Max(0.01f, minScaleMultiplier);
        float maximumMultiplier = Mathf.Max(minimumMultiplier, maxScaleMultiplier);
        // X：靠近正方向（ratio 0）用较大倍数。Z：靠近正方向用较小倍数。
        multiplier = useXAxis
            ? Mathf.Lerp(maximumMultiplier, minimumMultiplier, distanceRatio)
            : Mathf.Lerp(minimumMultiplier, maximumMultiplier, distanceRatio);
        return true;
    }

    bool TryGetAxisRange(bool useXAxis, Bounds fallbackBounds, out float minimum, out float maximum)
    {
        Vector3 axis = useXAxis ? transform.right : transform.forward;
        axis.y = 0f;
        if (axis.sqrMagnitude <= Mathf.Epsilon)
        {
            axis = useXAxis ? Vector3.right : Vector3.forward;
        }

        axis.Normalize();
        bool found = false;
        minimum = 0f;
        maximum = 0f;
        Collider[] ownColliders = GetComponents<Collider>();
        if (!EncapsulateAxis(ownColliders, axis, ref found, ref minimum, ref maximum))
        {
            EncapsulateAxis(GetComponentsInChildren<Collider>(true), axis, ref found, ref minimum, ref maximum);
        }

        if (!found)
        {
            ProjectBounds(fallbackBounds, axis, out minimum, out maximum);
            found = maximum - minimum > Mathf.Epsilon;
        }

        return found;
    }

    static bool EncapsulateAxis(
        Collider[] colliders,
        Vector3 axis,
        ref bool found,
        ref float minimum,
        ref float maximum)
    {
        if (colliders == null)
        {
            return found;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            ProjectBounds(collider.bounds, axis, out float min, out float max);
            if (!found)
            {
                minimum = min;
                maximum = max;
                found = true;
                continue;
            }

            minimum = Mathf.Min(minimum, min);
            maximum = Mathf.Max(maximum, max);
        }

        return found;
    }

    static void ProjectBounds(Bounds bounds, Vector3 axis, out float minimum, out float maximum)
    {
        float center = Vector3.Dot(bounds.center, axis);
        float extent =
            Mathf.Abs(axis.x) * bounds.extents.x +
            Mathf.Abs(axis.y) * bounds.extents.y +
            Mathf.Abs(axis.z) * bounds.extents.z;
        minimum = center - extent;
        maximum = center + extent;
    }
}
