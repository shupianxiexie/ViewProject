using UnityEngine;

public static class ViewWorldBounds
{
    public static Bounds Get(Transform target)
    {
        Bounds bounds = new Bounds(target.position, Vector3.zero);
        bool hasBounds = false;

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
        {
            return bounds;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            bounds.size = Vector3.one;
        }

        return bounds;
    }

    public static Bounds GetSilhouette(Transform target)
    {
        Bounds bounds = new Bounds(target.position, Vector3.zero);
        bool hasBounds = false;
        bool grassGround = IsGrassGround(target);

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (grassGround && !IsGrassGroundCollisionPart(target, renderer.transform))
            {
                continue;
            }

            Encapsulate(ref bounds, ref hasBounds, renderer.bounds);
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (grassGround && !IsGrassGroundCollisionPart(target, collider.transform))
            {
                continue;
            }

            Encapsulate(ref bounds, ref hasBounds, collider.bounds);
        }

        if (!hasBounds)
        {
            return Get(target);
        }

        return bounds;
    }

    static bool IsGrassGround(Transform target)
    {
        return target != null &&
               target.name.IndexOf("GrassGround", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool IsGrassGroundCollisionPart(Transform root, Transform node)
    {
        Transform current = node;
        while (current != null)
        {
            string objectName = current.name;
            if (objectName.Equals("Cube", System.StringComparison.OrdinalIgnoreCase) ||
                objectName.IndexOf("cliff_blockHalf_rock", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            if (current == root)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    static void Encapsulate(ref Bounds bounds, ref bool hasBounds, Bounds next)
    {
        if (!hasBounds)
        {
            bounds = next;
            hasBounds = true;
            return;
        }

        bounds.Encapsulate(next);
    }

    public static Rect ProjectToView(Bounds bounds, View.State state)
    {
        if (state == View.State.Version2DX)
        {
            return Rect.MinMaxRect(bounds.min.z, bounds.min.y, bounds.max.z, bounds.max.y);
        }

        return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
    }

    public static float OverlapRatio(Rect occluder, Rect target)
    {
        if (!occluder.Overlaps(target, true))
        {
            return 0f;
        }

        float xMin = Mathf.Max(occluder.xMin, target.xMin);
        float xMax = Mathf.Min(occluder.xMax, target.xMax);
        float yMin = Mathf.Max(occluder.yMin, target.yMin);
        float yMax = Mathf.Min(occluder.yMax, target.yMax);
        float overlapWidth = Mathf.Max(0f, xMax - xMin);
        float overlapHeight = Mathf.Max(0f, yMax - yMin);
        float overlapArea = overlapWidth * overlapHeight;
        float targetArea = Mathf.Abs(target.width * target.height);

        if (targetArea <= 0.0001f)
        {
            return overlapArea > 0f ? 1f : 0f;
        }

        return overlapArea / targetArea;
    }

    public static float CloserDepthScore(Bounds bounds, View.State state)
    {
        // Camera2DX looks along -X from +X, so larger X is closer.
        // Camera2DZ looks along +Z from -Z, so smaller Z is closer.
        if (state == View.State.Version2DX)
        {
            return bounds.center.x;
        }

        return -bounds.center.z;
    }
}
