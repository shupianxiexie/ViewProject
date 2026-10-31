using UnityEngine;

/// <summary>
/// 挂到平台上后，角色站在这块平台上才能透视缩放。
/// 每块平台自己决定倍数，以及允许沿 X（Q）还是 Z（E）缩放。
/// 脚本放在碰撞体物体上，或放在它的父物体上。
/// </summary>
[DisallowMultipleComponent]
public class PerspectiveScalePlatform : MonoBehaviour
{
    [Header("缩放倍数")]
    [Tooltip("靠近世界坐标负方向边缘（X 更小，或 Z 更小）时的角色缩放倍数。")]
    [Min(0.01f)]
    public float minScaleMultiplier = 0.5f;

    [Tooltip("靠近世界坐标正方向边缘（X 更大，或 Z 更大）时的角色缩放倍数。")]
    [Min(0.01f)]
    public float maxScaleMultiplier = 2f;

    [Header("允许的轴")]
    [Tooltip("站在这块平台上时，允许按 Q，按角色在平台 X 轴上的位置缩放。")]
    public bool allowScaleByX = true;

    [Tooltip("站在这块平台上时，允许按 E，按角色在平台 Z 轴上的位置缩放。")]
    public bool allowScaleByZ = true;

    private void OnValidate()
    {
        minScaleMultiplier = Mathf.Max(0.01f, minScaleMultiplier);
        maxScaleMultiplier = Mathf.Max(minScaleMultiplier, maxScaleMultiplier);
    }

    /// <summary>
    /// 用脚下碰撞体此刻的包围盒，按离正方向边缘的距离算出缩放倍数。
    /// 这个轴被关掉，或这条边长度为 0 时返回 false。
    /// </summary>
    public bool TryGetMultiplier(
        Vector3 worldPosition,
        bool useXAxis,
        Bounds platformBounds,
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

        float coordinate = useXAxis ? worldPosition.x : worldPosition.z;
        float minimum = useXAxis ? platformBounds.min.x : platformBounds.min.z;
        float maximum = useXAxis ? platformBounds.max.x : platformBounds.max.z;
        float axisLength = maximum - minimum;

        if (axisLength <= Mathf.Epsilon)
        {
            return false;
        }

        float distanceToPositiveEdge = Mathf.Abs(maximum - coordinate);
        float distanceRatio = Mathf.Clamp01(distanceToPositiveEdge / axisLength);
        float minimumMultiplier = Mathf.Max(0.01f, minScaleMultiplier);
        float maximumMultiplier = Mathf.Max(minimumMultiplier, maxScaleMultiplier);
        multiplier = Mathf.Lerp(maximumMultiplier, minimumMultiplier, distanceRatio);
        return true;
    }
}
