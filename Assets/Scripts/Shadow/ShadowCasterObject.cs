using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class ShadowCasterObject : MonoBehaviour
{
    public enum ShapeType
    {
        Box,
        Stone
    }

    public ShapeType shapeType = ShapeType.Stone;
    public Collider sourceCollider;
    public MeshFilter sourceMesh;
    public int stoneVertexStride = 4;

    [Header("Shadow Look")]
    public bool overrideShadowLook;
    public Vector2 shadowSize = new Vector2(10f, 10f);
    public Vector2 shadowOffset;

    void Awake()
    {
        EnsureSources();
    }

    public void GetShadowLook(
        Vector2 defaultSize,
        Vector2 defaultOffset,
        out Vector2 size,
        out Vector2 offset)
    {
        size = overrideShadowLook ? shadowSize : defaultSize;
        offset = overrideShadowLook ? shadowOffset : defaultOffset;
        size.x = Mathf.Max(0.1f, size.x);
        size.y = Mathf.Max(0.1f, size.y);
    }

    public bool CanCastShadow()
    {
        EnsureSources();

        Box heldBox = GetComponent<Box>();
        if (heldBox != null && heldBox.IsHeld)
        {
            //UnityEngine.Debug.Log("ShadowCasterObject: Cannot cast shadow because the box is held.");
            return false;
        }
        //UnityEngine.Debug.Log(sourceCollider != null);
        return sourceCollider != null && sourceCollider.enabled;
    }

    public List<Vector3> GetWorldSamplePoints()
    {
        EnsureSources();

        if (shapeType == ShapeType.Box)
        {
            return GetBoxPoints();
        }

        return GetStonePoints();
    }

    public void ConfigureAsBox(Collider collider)
    {
        shapeType = ShapeType.Box;
        sourceCollider = collider;
        sourceMesh = GetComponent<MeshFilter>();
    }

    public void ConfigureAsStone(Collider collider)
    {
        shapeType = ShapeType.Stone;
        sourceCollider = collider;
        sourceMesh = GetComponent<MeshFilter>();
        if (sourceMesh == null)
        {
            sourceMesh = GetComponentInChildren<MeshFilter>();
        }
    }

    void EnsureSources()
    {
        if (sourceCollider == null)
        {
            sourceCollider = GetComponent<Collider>();
        }

        if (sourceMesh == null)
        {
            sourceMesh = GetComponent<MeshFilter>();
            if (sourceMesh == null)
            {
                sourceMesh = GetComponentInChildren<MeshFilter>();
            }
        }

        if (GetComponent<Box>() != null)
        {
            shapeType = ShapeType.Box;
        }
    }

    List<Vector3> GetBoxPoints()
    {
        List<Vector3> points = new List<Vector3>(8);
        BoxCollider boxCollider = sourceCollider as BoxCollider;
        if (boxCollider != null)
        {
            Vector3 center = boxCollider.center;
            Vector3 extents = boxCollider.size * 0.5f;
            Transform boxTransform = boxCollider.transform;

            for (int i = 0; i < 8; i++)
            {
                Vector3 localCorner = center + new Vector3(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z);
                points.Add(boxTransform.TransformPoint(localCorner));
            }

            return points;
        }

        return GetBoundsCorners(sourceCollider.bounds);
    }

    List<Vector3> GetStonePoints()
    {
        List<Vector3> points = new List<Vector3>();
        Mesh mesh = sourceMesh != null ? sourceMesh.sharedMesh : null;
        if (mesh != null && mesh.vertexCount > 0)
        {
            Vector3[] vertices = mesh.vertices;
            int stride = Mathf.Max(1, stoneVertexStride);
            Transform meshTransform = sourceMesh.transform;

            for (int i = 0; i < vertices.Length; i += stride)
            {
                points.Add(meshTransform.TransformPoint(vertices[i]));
            }

            if (points.Count > 0)
            {
                return points;
            }
        }

        if (sourceCollider != null)
        {
            return GetBoundsCorners(sourceCollider.bounds);
        }

        return points;
    }

    static List<Vector3> GetBoundsCorners(Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        List<Vector3> points = new List<Vector3>(8);

        for (int i = 0; i < 8; i++)
        {
            points.Add(center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z));
        }

        return points;
    }
}
