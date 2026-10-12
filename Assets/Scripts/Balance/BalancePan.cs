using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BalancePan : MonoBehaviour
{
    public enum PanSide
    {
        Left,
        Right,
    }

    public PanSide side = PanSide.Left;
    public BalanceScale balanceScale;

    [Tooltip("Extra vertical padding used when detecting objects standing on this pan.")]
    public float detectionHeightPadding = 0.35f;

    private sealed class TrackedWeight
    {
        public float weight;
    }

    private readonly Dictionary<GameObject, TrackedWeight> weightedObjects =
        new Dictionary<GameObject, TrackedWeight>();
    private readonly HashSet<GameObject> currentRoots = new HashSet<GameObject>();
    private readonly List<GameObject> updateBuffer = new List<GameObject>();
    private Collider panCollider;

    void Awake()
    {
        // Do not modify Collider.isTrigger; keep whatever is set in the scene.
        panCollider = GetComponent<Collider>();

        if (balanceScale == null)
        {
            balanceScale = GetComponentInParent<BalanceScale>();
        }
    }

    void FixedUpdate()
    {
        RefreshOverlaps();
        SyncWeights();
    }

    void OnDisable()
    {
        updateBuffer.Clear();
        updateBuffer.AddRange(weightedObjects.Keys);

        foreach (GameObject root in updateBuffer)
        {
            RemoveTrackedObject(root);
        }

        currentRoots.Clear();
    }

    void RefreshOverlaps()
    {
        currentRoots.Clear();

        if (panCollider == null)
        {
            return;
        }

        Bounds bounds = panCollider.bounds;
        Vector3 center = bounds.center + Vector3.up * (detectionHeightPadding * 0.5f);
        Vector3 halfExtents = bounds.extents + new Vector3(0.05f, detectionHeightPadding * 0.5f, 0.05f);

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            panCollider.transform.rotation,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit == null || hit == panCollider)
            {
                continue;
            }

            if (!TryGetWeightedRoot(hit, out GameObject root))
            {
                continue;
            }

            currentRoots.Add(root);
        }

        updateBuffer.Clear();
        updateBuffer.AddRange(weightedObjects.Keys);
        foreach (GameObject root in updateBuffer)
        {
            if (!currentRoots.Contains(root))
            {
                RemoveTrackedObject(root);
            }
        }

        foreach (GameObject root in currentRoots)
        {
            if (weightedObjects.ContainsKey(root))
            {
                continue;
            }

            float weight = GetCurrentWeight(root);
            if (weight <= 0f)
            {
                continue;
            }

            weightedObjects.Add(root, new TrackedWeight { weight = weight });
            balanceScale?.RegisterWeight(side, weight);
        }
    }

    void SyncWeights()
    {
        updateBuffer.Clear();
        updateBuffer.AddRange(weightedObjects.Keys);

        foreach (GameObject root in updateBuffer)
        {
            if (root == null)
            {
                RemoveTrackedObject(root);
                continue;
            }

            TrackedWeight trackedWeight = weightedObjects[root];
            float currentWeight = GetCurrentWeight(root);
            float difference = currentWeight - trackedWeight.weight;

            if (Mathf.Abs(difference) <= Mathf.Epsilon)
            {
                continue;
            }

            balanceScale?.AdjustWeight(side, difference);
            trackedWeight.weight = currentWeight;
        }
    }

    void RemoveTrackedObject(GameObject root)
    {
        if (!weightedObjects.TryGetValue(root, out TrackedWeight trackedWeight))
        {
            return;
        }

        balanceScale?.UnregisterWeight(side, trackedWeight.weight);
        weightedObjects.Remove(root);
    }

    bool TryGetWeightedRoot(Collider other, out GameObject root)
    {
        root = null;
        if (other == null)
        {
            return false;
        }

        Box box = other.GetComponentInParent<Box>();
        if (box != null)
        {
            root = box.gameObject;
            return true;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            root = player.gameObject;
            return true;
        }

        WeightedObject weightedObject = other.GetComponentInParent<WeightedObject>();
        if (weightedObject != null)
        {
            root = weightedObject.gameObject;
            return true;
        }

        return false;
    }

    float GetCurrentWeight(GameObject root)
    {
        Box box = root.GetComponent<Box>();
        if (box != null)
        {
            return box.IsHeld ? 0f : Mathf.Max(0f, box.weight);
        }

        PlayerController player = root.GetComponent<PlayerController>();
        if (player != null)
        {
            return Mathf.Max(0f, player.weight);
        }

        WeightedObject weightedObject = root.GetComponent<WeightedObject>();
        return weightedObject != null ? Mathf.Max(0f, weightedObject.weight) : 0f;
    }
}
