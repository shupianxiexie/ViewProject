using UnityEngine;

public class ShadowPlatform : MonoBehaviour
{
    ShadowCurtain curtain;
    View playerView;
    MeshRenderer meshRenderer;
    BoxCollider boxCollider;

    public bool MatchesView(View.State state)
    {
        return curtain != null && curtain.activeView == state;
    }

    public void Initialize(ShadowCurtain targetCurtain, View view, Material material)
    {
        curtain = targetCurtain;
        playerView = view;

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        if (meshFilter.sharedMesh == null)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshFilter.sharedMesh = cube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(cube);
        }

        meshRenderer = gameObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        boxCollider = gameObject.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = gameObject.AddComponent<BoxCollider>();
        }

        MeshCollider meshCollider = gameObject.GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            Destroy(meshCollider);
        }

        meshRenderer.sharedMaterial = material;
        boxCollider.enabled = false;
        gameObject.tag = "Ground";
        gameObject.layer = 0;
    }

    public void PlaceOnCurtain(Vector3 worldCenter, Vector2 size, float thickness)
    {
        if (curtain == null)
        {
            UnityEngine.Debug.Log("curtain=null");
            return;
        }

        transform.SetParent(null, true);
        transform.position = worldCenter;
        transform.rotation = curtain.transform.rotation;
        transform.localScale = new Vector3(
            Mathf.Max(0.1f, size.x),
            Mathf.Max(0.1f, size.y),
            Mathf.Max(0.02f, thickness));
        UpdateState();
    }

    public void ApplyLook(Material material, Color color)
    {
        if (meshRenderer == null)
        {
            return;
        }

        if (material != null)
        {
            meshRenderer.sharedMaterial = material;
        }

        if (meshRenderer.material != null)
        {
            meshRenderer.material.color = color;
        }
    }

    public void UpdateState()
    {
        bool matching2D =
            playerView != null &&
            curtain != null &&
            playerView.currentState == curtain.activeView;
        bool in3D =
            playerView == null ||
            playerView.currentState == View.State.Version3D;

        if (meshRenderer != null)
        {
            meshRenderer.enabled = in3D || matching2D;
        }

        if (boxCollider != null && in3D)
        {
            boxCollider.enabled = false;
        }
    }
}
