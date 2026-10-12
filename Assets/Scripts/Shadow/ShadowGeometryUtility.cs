using System.Collections.Generic;
using UnityEngine;

public static class ShadowGeometryUtility
{
    const float DuplicateEpsilon = 0.0001f;

    public static List<Vector2> CreateOutline(
        List<Vector2> projectedPoints,
        ShadowCasterObject.ShapeType shape)
    {
        List<Vector2> hull = BuildConvexHull(projectedPoints);

        if (hull.Count < 3)
        {
            return hull;
        }

        if (shape == ShadowCasterObject.ShapeType.Box)
        {
            return ConvertToFourPoints(hull);
        }

        return SimplifyPolygon(hull, 10);
    }

    public static List<Vector2> BuildConvexHull(List<Vector2> points)
    {
        List<Vector2> sorted = new List<Vector2>(points.Count);
        for (int i = 0; i < points.Count; i++)
        {
            AddUniquePoint(sorted, points[i]);
        }

        if (sorted.Count <= 3)
        {
            return sorted;
        }

        sorted.Sort((a, b) =>
        {
            int compareX = a.x.CompareTo(b.x);
            return compareX != 0 ? compareX : a.y.CompareTo(b.y);
        });

        List<Vector2> lower = new List<Vector2>();
        for (int i = 0; i < sorted.Count; i++)
        {
            Vector2 point = sorted[i];
            while (lower.Count >= 2 &&
                   Cross(lower[lower.Count - 2], lower[lower.Count - 1], point) <= 0f)
            {
                lower.RemoveAt(lower.Count - 1);
            }

            lower.Add(point);
        }

        List<Vector2> upper = new List<Vector2>();
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            Vector2 point = sorted[i];
            while (upper.Count >= 2 &&
                   Cross(upper[upper.Count - 2], upper[upper.Count - 1], point) <= 0f)
            {
                upper.RemoveAt(upper.Count - 1);
            }

            upper.Add(point);
        }

        lower.RemoveAt(lower.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        lower.AddRange(upper);
        return lower;
    }

    public static List<Vector2> ConvertToFourPoints(List<Vector2> hull)
    {
        if (hull == null || hull.Count == 0)
        {
            return new List<Vector2>();
        }

        float minX = hull[0].x;
        float maxX = hull[0].x;
        float minY = hull[0].y;
        float maxY = hull[0].y;

        for (int i = 1; i < hull.Count; i++)
        {
            minX = Mathf.Min(minX, hull[i].x);
            maxX = Mathf.Max(maxX, hull[i].x);
            minY = Mathf.Min(minY, hull[i].y);
            maxY = Mathf.Max(maxY, hull[i].y);
        }

        return new List<Vector2>
        {
            new Vector2(minX, minY),
            new Vector2(maxX, minY),
            new Vector2(maxX, maxY),
            new Vector2(minX, maxY)
        };
    }

    public static List<Vector2> SimplifyPolygon(List<Vector2> polygon, int maxPoints)
    {
        if (polygon == null || polygon.Count <= maxPoints || maxPoints < 3)
        {
            return polygon;
        }

        List<Vector2> simplified = new List<Vector2>(polygon);
        while (simplified.Count > maxPoints)
        {
            int removeIndex = 1;
            float smallestArea = float.PositiveInfinity;

            for (int i = 0; i < simplified.Count; i++)
            {
                Vector2 previous = simplified[(i - 1 + simplified.Count) % simplified.Count];
                Vector2 current = simplified[i];
                Vector2 next = simplified[(i + 1) % simplified.Count];
                float area = Mathf.Abs(Cross(previous, current, next));
                if (area < smallestArea)
                {
                    smallestArea = area;
                    removeIndex = i;
                }
            }

            simplified.RemoveAt(removeIndex);
        }

        return simplified;
    }

    public static Mesh BuildPrismMesh(List<Vector2> polygon, float depth)
    {
        Mesh mesh = new Mesh();
        mesh.name = "ShadowPrism";

        if (polygon == null || polygon.Count < 3)
        {
            return mesh;
        }

        int count = polygon.Count;
        Vector3[] vertices = new Vector3[count * 2];
        for (int i = 0; i < count; i++)
        {
            vertices[i] = new Vector3(polygon[i].x, polygon[i].y, 0f);
            vertices[i + count] = new Vector3(polygon[i].x, polygon[i].y, depth);
        }

        List<int> triangles = new List<int>((count - 2) * 12 + count * 6);

        for (int i = 1; i < count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i + 1);
            triangles.Add(i);

            triangles.Add(count);
            triangles.Add(count + i);
            triangles.Add(count + i + 1);
        }

        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            int frontA = i;
            int frontB = next;
            int backA = i + count;
            int backB = next + count;

            triangles.Add(frontA);
            triangles.Add(frontB);
            triangles.Add(backB);

            triangles.Add(frontA);
            triangles.Add(backB);
            triangles.Add(backA);
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static float Cross(Vector2 origin, Vector2 a, Vector2 b)
    {
        return (a.x - origin.x) * (b.y - origin.y) -
               (a.y - origin.y) * (b.x - origin.x);
    }

    static void AddUniquePoint(List<Vector2> points, Vector2 candidate)
    {
        for (int i = 0; i < points.Count; i++)
        {
            if ((points[i] - candidate).sqrMagnitude <= DuplicateEpsilon)
            {
                return;
            }
        }

        points.Add(candidate);
    }
}
