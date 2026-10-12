using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Toggles a character-scaling mode with Shift. While enabled, Q and E
/// scale the character from its distance to the platform's positive X and
/// positive Z edges respectively.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class CharacterPerspectiveScaler : MonoBehaviour
{
    [Header("Scale Multipliers")]
    [Tooltip("Scale multiplier used at the platform's negative-axis edge.")]
    [Min(0.01f)]
    public float minScaleMultiplier = 0.5f;

    [Tooltip("Scale multiplier used at the platform's positive-axis edge.")]
    [Min(0.01f)]
    public float maxScaleMultiplier = 2f;

    [Header("Platform Detection")]
    [Tooltip("Extra ray distance below the character collider.")]
    [Min(0f)]
    public float extraGroundCheckDistance = 0.5f;

    [Tooltip("Keep the character's feet at the same height after scaling.")]
    public bool keepFeetPosition = true;

    [Header("Input")]
    public KeyCode toggleModeKey = KeyCode.LeftShift;
    public KeyCode scaleByXKey = KeyCode.Q;
    public KeyCode scaleByZKey = KeyCode.E;

    [Header("Runtime")]
    [SerializeField]
    private bool scaleModeEnabled;

    private readonly Dictionary<Collider, Bounds> originalPlatformBounds =
        new Dictionary<Collider, Bounds>();

    private Collider characterCollider;
    private Rigidbody characterRigidbody;
    private PlayerController playerController;
    private Vector3 initialLocalScale;
    private float initialRigidbodyMass = 1f;
    private float initialGameplayWeight = 1f;

    public bool ScaleModeEnabled => scaleModeEnabled;

    private void Awake()
    {
        characterCollider = GetComponent<Collider>();
        characterRigidbody = GetComponent<Rigidbody>();
        playerController = GetComponent<PlayerController>();
        initialLocalScale = transform.localScale;

        if (characterRigidbody != null)
        {
            initialRigidbodyMass = characterRigidbody.mass;
        }

        if (playerController != null)
        {
            initialGameplayWeight = playerController.weight;
        }
    }

    private void Start()
    {
        CacheOriginalPlatformBounds();
    }

    private void OnValidate()
    {
        minScaleMultiplier = Mathf.Max(0.01f, minScaleMultiplier);
        maxScaleMultiplier = Mathf.Max(minScaleMultiplier, maxScaleMultiplier);
        extraGroundCheckDistance = Mathf.Max(0f, extraGroundCheckDistance);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleModeKey))
        {
            scaleModeEnabled = !scaleModeEnabled;
        }

        if (!scaleModeEnabled)
        {
            return;
        }

        if (Input.GetKeyDown(scaleByXKey))
        {
            ScaleFromPositiveEdge(useXAxis: true);
        }

        if (Input.GetKeyDown(scaleByZKey))
        {
            ScaleFromPositiveEdge(useXAxis: false);
        }
    }

    private void CacheOriginalPlatformBounds()
    {
        originalPlatformBounds.Clear();

        foreach (Floor floor in Floor.allFloors)
        {
            if (floor == null)
            {
                continue;
            }

            foreach (Collider platformCollider in floor.GetComponentsInChildren<Collider>())
            {
                if (platformCollider != null && !platformCollider.isTrigger)
                {
                    originalPlatformBounds[platformCollider] = platformCollider.bounds;
                }
            }
        }
    }

    private void ScaleFromPositiveEdge(bool useXAxis)
    {
        if (!TryFindCurrentPlatform(out Collider platformCollider))
        {
            Debug.LogWarning(
                $"{nameof(CharacterPerspectiveScaler)} could not find a platform below {name}.",
                this);
            return;
        }

        Bounds platformBounds = GetOriginalPlatformBounds(platformCollider);
        float coordinate = useXAxis ? transform.position.x : transform.position.z;
        float minimum = useXAxis ? platformBounds.min.x : platformBounds.min.z;
        float maximum = useXAxis ? platformBounds.max.x : platformBounds.max.z;
        float axisLength = maximum - minimum;

        if (axisLength <= Mathf.Epsilon)
        {
            return;
        }

        float distanceToPositiveEdge = Mathf.Abs(maximum - coordinate);
        float distanceRatio = Mathf.Clamp01(distanceToPositiveEdge / axisLength);
        float multiplier = Mathf.Lerp(
            maxScaleMultiplier,
            minScaleMultiplier,
            distanceRatio);

        ApplyScale(multiplier);
    }

    private bool TryFindCurrentPlatform(out Collider platformCollider)
    {
        platformCollider = null;

        Bounds characterBounds = characterCollider != null
            ? characterCollider.bounds
            : new Bounds(transform.position, Vector3.zero);
        Vector3 rayOrigin = characterBounds.center + Vector3.up * 0.05f;
        float rayDistance = characterBounds.extents.y + extraGroundCheckDistance + 0.05f;

        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            rayDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (!IsPlatform(hit.collider) || hit.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = hit.distance;
            platformCollider = hit.collider;
        }

        return platformCollider != null;
    }

    private static bool IsPlatform(Collider candidate)
    {
        return candidate != null &&
               (candidate.CompareTag("Ground") ||
                candidate.GetComponentInParent<Floor>() != null ||
                candidate.GetComponentInParent<Box>() != null);
    }

    private Bounds GetOriginalPlatformBounds(Collider platformCollider)
    {
        if (originalPlatformBounds.TryGetValue(platformCollider, out Bounds bounds))
        {
            return bounds;
        }

        // Movable boxes and platforms without Floor use their current bounds.
        return platformCollider.bounds;
    }

    private void ApplyScale(float multiplier)
    {
        float previousFeetHeight = characterCollider != null
            ? characterCollider.bounds.min.y
            : transform.position.y;

        transform.localScale = initialLocalScale * multiplier;

        if (characterRigidbody != null)
        {
            characterRigidbody.mass = initialRigidbodyMass * multiplier;
        }

        if (playerController != null)
        {
            playerController.weight = initialGameplayWeight * multiplier;
        }

        if (!keepFeetPosition || characterCollider == null)
        {
            return;
        }

        Physics.SyncTransforms();
        float heightCorrection = previousFeetHeight - characterCollider.bounds.min.y;
        Vector3 correctedPosition = transform.position + Vector3.up * heightCorrection;

        if (characterRigidbody != null)
        {
            characterRigidbody.position = correctedPosition;
        }
        else
        {
            transform.position = correctedPosition;
        }
    }
}
