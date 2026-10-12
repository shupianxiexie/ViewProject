using UnityEngine;

public class ShadowCurtain : MonoBehaviour
{
    public enum CurtainAxis
    {
        NegativeX,
        NegativeZ
    }

    public CurtainAxis axis;
    public View.State activeView;
    public Vector2 curtainSize = new Vector2(40f, 20f);
    public float platformDepth = 0.2f;
    public float walkableDepth = 1000f;

    public bool AcceptsDirection(Vector3 rayDirection)
    {
        if (rayDirection.sqrMagnitude < 0.000001f)
        {
            return false;
        }

        rayDirection.Normalize();

        switch (axis)
        {
            case CurtainAxis.NegativeX:
                return rayDirection.x < -0.001f;
            case CurtainAxis.NegativeZ:
                return rayDirection.z > 0.001f;
            default:
                return false;
        }
    }

    public bool TryProjectPoint(
        Vector3 lightPosition,
        Vector3 objectPoint,
        out Vector3 projectedPoint)
    {
        projectedPoint = default;

        Vector3 toPoint = objectPoint - lightPosition;
        if (toPoint.sqrMagnitude < 0.000001f)
        {
            return false;
        }

        Vector3 rayDirection = toPoint.normalized;
        if (!AcceptsDirection(rayDirection))
        {
            return false;
        }

        Plane plane = new Plane(transform.forward, transform.position);
        Ray ray = new Ray(lightPosition, rayDirection);
        if (!plane.Raycast(ray, out float distance) || distance <= 0f)
        {
            return false;
        }

        projectedPoint = ray.GetPoint(distance);
        return IsInsideCurtain(projectedPoint);
    }

    public Vector2 WorldToCurtainPoint(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        return new Vector2(local.x, local.y);
    }

    public Vector3 CurtainToWorldPoint(Vector2 point, float depth = 0f)
    {
        return transform.TransformPoint(new Vector3(point.x, point.y, depth));
    }

    bool IsInsideCurtain(Vector3 worldPoint)
    {
        Vector3 local = transform.InverseTransformPoint(worldPoint);
        float halfWidth = Mathf.Max(0.01f, curtainSize.x) * 0.5f;
        float halfHeight = Mathf.Max(0.01f, curtainSize.y) * 0.5f;
        return Mathf.Abs(local.x) <= halfWidth && Mathf.Abs(local.y) <= halfHeight;
    }
}
