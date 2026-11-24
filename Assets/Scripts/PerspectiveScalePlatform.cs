using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在一块触发区域上。同一物体上要有勾选 Is Trigger 的碰撞体，碰撞体的范围就是缩放范围。
/// 只有角色在这块区域里切换进入 2D 的那一刻才定倍数，之后保持不变。
/// 2DX 用 X：靠近正方向变大。2DZ 用 Z：远离正方向变大。
/// 回到 3D 后恢复初始大小。
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
    [Tooltip("进入 2DX 时，靠近区域 X 正方向变大，远离变小。只在切入的那一刻计算一次。")]
    public bool allowScaleByX = true;

    [Tooltip("进入 2DZ 时，远离区域 Z 正方向变大，靠近变小。只在切入的那一刻计算一次。")]
    public bool allowScaleByZ = true;

    static readonly List<PerspectiveScalePlatform> allZones = new List<PerspectiveScalePlatform>();

    readonly List<Collider> triggerColliders = new List<Collider>();
    readonly List<CharacterPerspectiveScaler> occupants = new List<CharacterPerspectiveScaler>();
    bool warnedMissingTrigger;
    bool didInitialOverlap;

    void Awake()
    {
        CacheTriggers();
    }

    void OnEnable()
    {
        if (!allZones.Contains(this))
        {
            allZones.Add(this);
        }
    }

    void Start()
    {
        CacheTriggers();
        RegisterPlayersAlreadyInside();
        didInitialOverlap = triggerColliders.Count > 0;
    }

    void LateUpdate()
    {
        CacheTriggers();
        KeepTriggersEnabled();
        if (!didInitialOverlap && triggerColliders.Count > 0)
        {
            RegisterPlayersAlreadyInside();
            didInitialOverlap = true;
        }
    }

    void OnDisable()
    {
        allZones.Remove(this);
        for (int i = occupants.Count - 1; i >= 0; i--)
        {
            CharacterPerspectiveScaler scaler = occupants[i];
            if (scaler != null)
            {
                scaler.ExitScaleZone(this);
            }
        }

        occupants.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        TryAdmit(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryAdmit(other);
    }

    void OnTriggerExit(Collider other)
    {
        if (other == null)
        {
            return;
        }

        CharacterPerspectiveScaler scaler = other.GetComponent<CharacterPerspectiveScaler>();
        if (scaler == null || !occupants.Remove(scaler))
        {
            return;
        }

        scaler.ExitScaleZone(this);
    }

    void OnValidate()
    {
        minScaleMultiplier = Mathf.Max(0.01f, minScaleMultiplier);
        maxScaleMultiplier = Mathf.Max(minScaleMultiplier, maxScaleMultiplier);
    }

    public bool AllowsAxis(bool useXAxis)
    {
        return useXAxis ? allowScaleByX : allowScaleByZ;
    }

    public static PerspectiveScalePlatform FindOverlapping(CharacterPerspectiveScaler scaler)
    {
        for (int i = allZones.Count - 1; i >= 0; i--)
        {
            PerspectiveScalePlatform zone = allZones[i];
            if (zone == null)
            {
                allZones.RemoveAt(i);
                continue;
            }

            if (zone.OverlapsCharacter(scaler))
            {
                return zone;
            }
        }

        return null;
    }

    public bool OverlapsCharacter(CharacterPerspectiveScaler scaler)
    {
        if (scaler == null)
        {
            return false;
        }

        Collider body = scaler.GetComponent<Collider>();
        if (body == null)
        {
            return false;
        }

        if (triggerColliders.Count == 0)
        {
            CacheTriggers();
        }

        for (int i = 0; i < triggerColliders.Count; i++)
        {
            Collider[] hits = OverlapTrigger(triggerColliders[i]);
            for (int h = 0; h < hits.Length; h++)
            {
                Collider hit = hits[h];
                if (hit == null)
                {
                    continue;
                }

                if (hit == body || hit.GetComponent<CharacterPerspectiveScaler>() == scaler)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 按这块触发区域在该轴上的范围计算倍数。
    /// X 靠近正方向变大；Z 远离正方向变大。
    /// </summary>
    public bool TryGetMultiplier(Vector3 worldPosition, bool useXAxis, out float multiplier)
    {
        multiplier = 1f;
        if (!AllowsAxis(useXAxis))
        {
            return false;
        }

        if (!TryGetAxisRange(useXAxis, out float minimum, out float maximum))
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

    void CacheTriggers()
    {
        triggerColliders.Clear();
        Collider[] colliders = GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && collider.isTrigger)
            {
                triggerColliders.Add(collider);
            }
        }

        if (triggerColliders.Count == 0 && !warnedMissingTrigger && Application.isPlaying)
        {
            warnedMissingTrigger = true;
            Debug.LogWarning(
                "缩放区域需要在同一个物体上添加 Collider，并勾选 Is Trigger。",
                this);
        }

        if (triggerColliders.Count > 0)
        {
            warnedMissingTrigger = false;
        }
    }

    // 进入 2D 时，压扁流程会关掉区域内的碰撞体。触发体要留着，人才能走进来。
    void KeepTriggersEnabled()
    {
        for (int i = 0; i < triggerColliders.Count; i++)
        {
            Collider collider = triggerColliders[i];
            if (collider != null && !collider.enabled)
            {
                collider.enabled = true;
            }
        }
    }

    void RegisterPlayersAlreadyInside()
    {
        for (int i = 0; i < triggerColliders.Count; i++)
        {
            Collider[] hits = OverlapTrigger(triggerColliders[i]);
            for (int h = 0; h < hits.Length; h++)
            {
                TryAdmit(hits[h]);
            }
        }
    }

    void TryAdmit(Collider other)
    {
        if (other == null)
        {
            return;
        }

        CharacterPerspectiveScaler scaler = other.GetComponent<CharacterPerspectiveScaler>();
        if (scaler == null || occupants.Contains(scaler))
        {
            return;
        }

        occupants.Add(scaler);
        scaler.EnterScaleZone(this);
    }

    bool TryGetAxisRange(bool useXAxis, out float minimum, out float maximum)
    {
        minimum = 0f;
        maximum = 0f;
        bool found = false;
        if (triggerColliders.Count == 0)
        {
            CacheTriggers();
        }

        for (int i = 0; i < triggerColliders.Count; i++)
        {
            Collider collider = triggerColliders[i];
            if (collider == null)
            {
                continue;
            }

            IncludeCollider(collider, useXAxis, ref found, ref minimum, ref maximum);
        }

        return found && maximum - minimum > Mathf.Epsilon;
    }

    void IncludeCollider(
        Collider collider,
        bool useXAxis,
        ref bool found,
        ref float minimum,
        ref float maximum)
    {
        BoxCollider box = collider as BoxCollider;
        if (box != null)
        {
            Vector3 center = box.center;
            Vector3 extents = box.size * 0.5f;
            float minValue = useXAxis ? center.x - extents.x : center.z - extents.z;
            float maxValue = useXAxis ? center.x + extents.x : center.z + extents.z;
            IncludeValue(minValue, ref found, ref minimum, ref maximum);
            IncludeValue(maxValue, ref found, ref minimum, ref maximum);
            return;
        }

        Bounds worldBounds = collider.bounds;
        Vector3 worldCenter = worldBounds.center;
        Vector3 worldExtents = worldBounds.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = worldCenter + new Vector3(
                (i & 1) == 0 ? -worldExtents.x : worldExtents.x,
                (i & 2) == 0 ? -worldExtents.y : worldExtents.y,
                (i & 4) == 0 ? -worldExtents.z : worldExtents.z);
            Vector3 local = transform.InverseTransformPoint(corner);
            IncludeValue(useXAxis ? local.x : local.z, ref found, ref minimum, ref maximum);
        }
    }

    static void IncludeValue(
        float value,
        ref bool found,
        ref float minimum,
        ref float maximum)
    {
        if (!found)
        {
            minimum = value;
            maximum = value;
            found = true;
            return;
        }

        minimum = Mathf.Min(minimum, value);
        maximum = Mathf.Max(maximum, value);
    }

    static Collider[] OverlapTrigger(Collider trigger)
    {
        if (trigger == null)
        {
            return new Collider[0];
        }

        BoxCollider box = trigger as BoxCollider;
        if (box != null)
        {
            Vector3 lossy = box.transform.lossyScale;
            Vector3 half = new Vector3(
                Mathf.Abs(box.size.x * lossy.x) * 0.5f,
                Mathf.Abs(box.size.y * lossy.y) * 0.5f,
                Mathf.Abs(box.size.z * lossy.z) * 0.5f);
            if (half.sqrMagnitude <= Mathf.Epsilon)
            {
                return new Collider[0];
            }

            return Physics.OverlapBox(
                box.transform.TransformPoint(box.center),
                half,
                box.transform.rotation,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
        }

        Bounds bounds = trigger.bounds;
        if (bounds.extents.sqrMagnitude <= Mathf.Epsilon)
        {
            return new Collider[0];
        }

        return Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            Quaternion.identity,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
    }
}
