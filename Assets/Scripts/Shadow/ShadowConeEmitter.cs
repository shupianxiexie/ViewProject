using System.Collections.Generic;
using UnityEngine;

public class ShadowConeEmitter : MonoBehaviour
{
    static bool hasBootstrapped;

    [Header("Cone")]
    [Range(1f, 179f)]
    public float coneAngle = 45f;
    public float range = 20f;
    public LayerMask casterMask = ~0;

    [Header("Curtains")]
    public ShadowCurtain negativeXCurtain;
    public ShadowCurtain negativeZCurtain;

    [Header("References")]
    public View playerView;
    public Material shadowMaterial;

    [Header("Shadow Look")]
    public Vector2 shadowSize = new Vector2(10f, 10f);
    public Vector2 shadowOffset;
    public float shadowThickness = 0.05f;
    public Color shadowColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);

    [Header("Update")]
    public float refreshInterval = 0.05f;

    [Header("Auto Detect")]
    public string[] stoneNameKeywords = { "stone", "rock", "Stone", "Rock" };

    readonly Dictionary<string, ShadowPlatform> platforms =
        new Dictionary<string, ShadowPlatform>();

    float nextRefreshTime;
    Material runtimeMaterial;

    public static void EnsureInitialized(View view)
    {
        if (view == null)
        {
            return;
        }

        ShadowConeEmitter existing = FindObjectOfType<ShadowConeEmitter>();
        if (existing != null)
        {
            if (existing.playerView == null)
            {
                existing.playerView = view;
            }

            existing.EnsureCurtains();
            return;
        }

        if (hasBootstrapped)
        {
            return;
        }

        Transform lightTransform = FindLightTransform();
        if (lightTransform == null)
        {
            return;
        }

        hasBootstrapped = true;
        ShadowConeEmitter emitter = lightTransform.GetComponent<ShadowConeEmitter>();
        if (emitter == null)
        {
            emitter = lightTransform.gameObject.AddComponent<ShadowConeEmitter>();
        }

        emitter.playerView = view;
        emitter.EnsureCurtains();
    }

    void Awake()
    {
        if (casterMask == 0)
        {
            casterMask = ~0;
        }
    }

    void Start()
    {
        if (playerView == null)
        {
            playerView = FindObjectOfType<View>();
        }

        EnsureCurtains();
        SyncSpotLight();
    }

    void Update()
    {
        RefreshShadows();
        UpdatePlatformStates();
        SyncSpotLight();
    }

    bool IsFlattenedView()
    {
        return playerView != null && playerView.currentState != View.State.Version3D;
    }

    void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    void RefreshShadows()
    {
        HashSet<string> activeKeys = new HashSet<string>();
        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            range,
            casterMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (!IsValidHit(hit))
            {
                continue;
            }

            ShadowCasterObject caster = GetOrCreateCaster(hit);
            if (caster == null || !caster.CanCastShadow() || !IsInsideCone(caster))
            {
                continue;
            }

            TryCreateShadow(caster, negativeXCurtain, activeKeys);
            TryCreateShadow(caster, negativeZCurtain, activeKeys);
        }

        RemoveInactiveShadows(activeKeys);
    }

    void TryCreateShadow(
        ShadowCasterObject caster,
        ShadowCurtain curtain,
        HashSet<string> activeKeys)
    {
        if (curtain == null)
        {
            return;
        }

        Vector3 referencePoint = caster.sourceCollider != null
            ? caster.sourceCollider.ClosestPoint(transform.position)
            : caster.transform.position;
        Vector3 centerDirection = referencePoint - transform.position;
        if (!curtain.AcceptsDirection(centerDirection))
        {
            return;
        }

        List<Vector3> worldPoints = caster.GetWorldSamplePoints();
        List<Vector2> projectedPoints = new List<Vector2>(worldPoints.Count);

        for (int i = 0; i < worldPoints.Count; i++)
        {
            if (curtain.TryProjectPoint(
                transform.position,
                worldPoints[i],
                out Vector3 projected))
            {
                projectedPoints.Add(curtain.WorldToCurtainPoint(projected));
            }
        }

        if (projectedPoints.Count == 0)
        {
            return;
        }

        Vector2 center = Vector2.zero;
        for (int i = 0; i < projectedPoints.Count; i++)
        {
            center += projectedPoints[i];
        }

        center /= projectedPoints.Count;
        Vector2 size = shadowSize;
        Vector2 offset = shadowOffset;
        if (caster.overrideShadowLook)
        {
            size = caster.shadowSize;
            offset = caster.shadowOffset;
        }

        size.x = Mathf.Max(0.1f, size.x);
        size.y = Mathf.Max(0.1f, size.y);

        center += offset;
        Vector3 worldCenter = curtain.CurtainToWorldPoint(center, shadowThickness * 0.5f);

        string key = caster.GetInstanceID() + "_" + curtain.GetInstanceID();
        activeKeys.Add(key);

        ShadowPlatform platform = GetOrCreatePlatform(key, curtain);
        platform.PlaceOnCurtain(worldCenter, size, shadowThickness);
        platform.ApplyLook(GetShadowMaterial(), shadowColor);
    }

    ShadowPlatform GetOrCreatePlatform(string key, ShadowCurtain curtain)
    {
        if (platforms.TryGetValue(key, out ShadowPlatform existing) && existing != null)
        {
            return existing;
        }

        GameObject platformObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platformObject.name = "ShadowPlatform";
        Object.Destroy(platformObject.GetComponent<MeshCollider>());

        ShadowPlatform platform = platformObject.AddComponent<ShadowPlatform>();
        platform.Initialize(curtain, playerView, GetShadowMaterial());
        platforms[key] = platform;
        return platform;
    }

    void RemoveInactiveShadows(HashSet<string> activeKeys)
    {
        List<string> staleKeys = new List<string>();
        foreach (KeyValuePair<string, ShadowPlatform> pair in platforms)
        {
            if (!activeKeys.Contains(pair.Key) || pair.Value == null)
            {
                staleKeys.Add(pair.Key);
            }
        }

        for (int i = 0; i < staleKeys.Count; i++)
        {
            string key = staleKeys[i];
            if (platforms.TryGetValue(key, out ShadowPlatform platform) && platform != null)
            {
                Destroy(platform.gameObject);
            }

            platforms.Remove(key);
        }
    }

    void UpdatePlatformStates()
    {
        foreach (ShadowPlatform platform in platforms.Values)
        {
            if (platform != null)
            {
                platform.UpdateState();
            }
        }
    }

    bool IsInsideCone(ShadowCasterObject caster)
    {
        Vector3 closest = caster.sourceCollider != null
            ? caster.sourceCollider.ClosestPoint(transform.position)
            : caster.transform.position;
        Vector3 direction = closest - transform.position;
        if (direction.sqrMagnitude > range * range)
        {
            return false;
        }

        float angle = Vector3.Angle(transform.forward, direction);
        return angle <= coneAngle * 0.5f;
    }

    bool IsValidHit(Collider hit)
    {
        if (hit == null || hit.transform.IsChildOf(transform))
        {
            return false;
        }

        if (hit.GetComponentInParent<ShadowPlatform>() != null ||
            hit.GetComponentInParent<ShadowCurtain>() != null ||
            hit.GetComponentInParent<PlayerController>() != null ||
            hit.GetComponentInParent<CarryLight>() != null)
        {
            return false;
        }

        return true;
    }

    ShadowCasterObject GetOrCreateCaster(Collider hit)
    {
        ShadowCasterObject caster = hit.GetComponentInParent<ShadowCasterObject>();
        if (caster != null)
        {
            return caster;
        }

        Box box = hit.GetComponentInParent<Box>();
        if (box != null)
        {
            return EnsureBoxCaster(box);
        }

        if (!MatchesStoneName(hit.transform))
        {
            return null;
        }

        caster = hit.gameObject.AddComponent<ShadowCasterObject>();
        caster.ConfigureAsStone(hit);
        return caster;
    }

    public static ShadowCasterObject EnsureBoxCaster(Box box)
    {
        if (box == null)
        {
            return null;
        }

        ShadowCasterObject caster = box.GetComponent<ShadowCasterObject>();
        if (caster == null)
        {
            caster = box.gameObject.AddComponent<ShadowCasterObject>();
        }

        caster.ConfigureAsBox(box.GetComponent<Collider>());
        return caster;
    }

    bool MatchesStoneName(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            string objectName = current.name;
            for (int i = 0; i < stoneNameKeywords.Length; i++)
            {
                if (!string.IsNullOrEmpty(stoneNameKeywords[i]) &&
                    objectName.IndexOf(stoneNameKeywords[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            current = current.parent;
        }

        return false;
    }

    void EnsureCurtains()
    {
        if (negativeXCurtain == null)
        {
            negativeXCurtain = FindCurtain(ShadowCurtain.CurtainAxis.NegativeX);
        }

        if (negativeZCurtain == null)
        {
            negativeZCurtain = FindCurtain(ShadowCurtain.CurtainAxis.NegativeZ);
        }

        Bounds levelBounds = GetLevelBounds();

        if (negativeXCurtain == null)
        {
            negativeXCurtain = CreateCurtain(
                "ShadowCurtainNegativeX",
                ShadowCurtain.CurtainAxis.NegativeX,
                View.State.Version2DX,
                new Vector3(levelBounds.min.x, levelBounds.center.y, levelBounds.center.z),
                Vector3.right,
                new Vector2(Mathf.Max(10f, levelBounds.size.z + 4f), Mathf.Max(10f, levelBounds.size.y + 8f)));
        }

        if (negativeZCurtain == null)
        {
            negativeZCurtain = CreateCurtain(
                "ShadowCurtainNegativeZ",
                ShadowCurtain.CurtainAxis.NegativeZ,
                View.State.Version2DZ,
                new Vector3(levelBounds.center.x, levelBounds.center.y, levelBounds.min.z),
                Vector3.forward,
                new Vector2(Mathf.Max(10f, levelBounds.size.x + 4f), Mathf.Max(10f, levelBounds.size.y + 8f)));
        }
    }

    static ShadowCurtain FindCurtain(ShadowCurtain.CurtainAxis axis)
    {
        ShadowCurtain[] curtains = FindObjectsOfType<ShadowCurtain>();
        for (int i = 0; i < curtains.Length; i++)
        {
            if (curtains[i] != null && curtains[i].axis == axis)
            {
                return curtains[i];
            }
        }

        string[] names = axis == ShadowCurtain.CurtainAxis.NegativeX
            ? new[] { "CurtainNegativeX", "ShadowCurtainNegativeX", "CurtainX" }
            : new[] { "CurtainNegativeZ", "ShadowCurtainNegativeZ", "CurtainZ" };

        for (int i = 0; i < names.Length; i++)
        {
            GameObject namedObject = GameObject.Find(names[i]);
            if (namedObject == null)
            {
                continue;
            }

            ShadowCurtain curtain = namedObject.GetComponent<ShadowCurtain>();
            if (curtain == null)
            {
                curtain = namedObject.AddComponent<ShadowCurtain>();
            }

            curtain.axis = axis;
            curtain.activeView = axis == ShadowCurtain.CurtainAxis.NegativeX
                ? View.State.Version2DX
                : View.State.Version2DZ;
            return curtain;
        }

        return null;
    }

    ShadowCurtain CreateCurtain(
        string objectName,
        ShadowCurtain.CurtainAxis axis,
        View.State activeView,
        Vector3 position,
        Vector3 forward,
        Vector2 size)
    {
        GameObject curtainObject = new GameObject(objectName);
        curtainObject.transform.position = position;
        curtainObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

        ShadowCurtain curtain = curtainObject.AddComponent<ShadowCurtain>();
        curtain.axis = axis;
        curtain.activeView = activeView;
        curtain.curtainSize = size;
        curtain.platformDepth = 0.2f;
        curtain.walkableDepth = 1000f;
        return curtain;
    }

    static Bounds GetLevelBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, new Vector3(20f, 10f, 20f));

        foreach (Floor floor in Floor.allFloors)
        {
            if (floor == null)
            {
                continue;
            }

            Collider floorCollider = floor.GetComponentInChildren<Collider>();
            if (floorCollider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = floorCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(floorCollider.bounds);
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(Vector3.zero, new Vector3(30f, 12f, 30f));
        }

        return bounds;
    }

    static Transform FindLightTransform()
    {
        GameObject namedLight = GameObject.Find("light");
        if (namedLight == null)
        {
            namedLight = GameObject.Find("Light");
        }

        if (namedLight != null)
        {
            return namedLight.transform;
        }

        Light[] lights = FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Spot)
            {
                return lights[i].transform;
            }
        }

        return null;
    }

    Material GetShadowMaterial()
    {
        if (shadowMaterial != null)
        {
            return shadowMaterial;
        }

        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Diffuse");
            }

            runtimeMaterial = new Material(shader)
            {
                color = shadowColor,
                name = "RuntimeShadowMaterial"
            };
        }
        else
        {
            runtimeMaterial.color = shadowColor;
        }

        return runtimeMaterial;
    }

    void SyncSpotLight()
    {
        Light spotLight = GetComponent<Light>();
        if (spotLight == null)
        {
            return;
        }

        spotLight.type = LightType.Spot;
        spotLight.spotAngle = coneAngle;
        spotLight.range = range;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.25f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawFrustum(Vector3.zero, coneAngle, range, 0.05f, 1f);
    }
}
