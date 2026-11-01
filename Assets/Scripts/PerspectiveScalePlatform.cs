using UnityEngine;

/// <summary>
/// 挂在一整块平台上，或挂在由多块小平台组成的父物体上。
/// 父物体不要加碰撞体，小平台上不要再挂这个脚本。
/// X：靠近正方向变大。Z：远离正方向变大。
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

    bool hasLocalRange;
    float localMinX;
    float localMaxX;
    float localMinZ;
    float localMaxZ;

    private void Start()
    {
        CacheLocalRange();
    }

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

        if (!hasLocalRange)
        {
            CacheLocalRange();
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

        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        float coordinate = useXAxis ? localPosition.x : localPosition.z;
        float distanceRatio = Mathf.Clamp01((maximum - coordinate) / axisLength);
        float minimumMultiplier = Mathf.Max(0.01f, minScaleMultiplier);
        float maximumMultiplier = Mathf.Max(minimumMultiplier, maxScaleMultiplier);
        // X：靠近正方向（ratio 0）用较大倍数。Z：靠近正方向用较小倍数。
        multiplier = useXAxis
            ? Mathf.Lerp(maximumMultiplier, minimumMultiplier, distanceRatio)
            : Mathf.Lerp(minimumMultiplier, maximumMultiplier, distanceRatio);
        return true;
    }

    // 进游戏时记下子平台还开着的碰撞体。2D 里这些碰撞体会被关掉，不能再拿来量长度。
    void CacheLocalRange()
    {
        hasLocalRange = false;
        localMinX = 0f;
        localMaxX = 0f;
        localMinZ = 0f;
        localMaxZ = 0f;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (!IsMeasurableCollider(collider))
            {
                continue;
            }

            IncludeWorldBounds(collider.bounds);
        }

        if (hasLocalRange)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.bounds.size.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            IncludeWorldBounds(renderer.bounds);
        }
    }

    bool TryGetAxisRange(bool useXAxis, Bounds fallbackBounds, out float minimum, out float maximum)
    {
        if (hasLocalRange)
        {
            minimum = useXAxis ? localMinX : localMinZ;
            maximum = useXAxis ? localMaxX : localMaxZ;
            return maximum - minimum > Mathf.Epsilon;
        }

        IncludeWorldBounds(fallbackBounds);
        minimum = useXAxis ? localMinX : localMinZ;
        maximum = useXAxis ? localMaxX : localMaxZ;
        return hasLocalRange && maximum - minimum > Mathf.Epsilon;
    }

    static bool IsMeasurableCollider(Collider collider)
    {
        return collider != null &&
               collider.enabled &&
               !collider.isTrigger &&
               collider.GetComponent<ViewScaleRoot>() == null &&
               collider.bounds.size.sqrMagnitude > 0.0001f;
    }

    void IncludeWorldBounds(Bounds worldBounds)
    {
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z);
            Vector3 local = transform.InverseTransformPoint(corner);
            if (!hasLocalRange)
            {
                localMinX = localMaxX = local.x;
                localMinZ = localMaxZ = local.z;
                hasLocalRange = true;
                continue;
            }

            localMinX = Mathf.Min(localMinX, local.x);
            localMaxX = Mathf.Max(localMaxX, local.x);
            localMinZ = Mathf.Min(localMinZ, local.z);
            localMaxZ = Mathf.Max(localMaxZ, local.z);
        }
    }
}
