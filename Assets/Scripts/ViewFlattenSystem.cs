using System.Collections.Generic;
using UnityEngine;

public class ViewFlattenSystem
{
    const float MovedEpsilon = 0.08f;
    const float DepthEpsilon = 0.01f;
    enum TargetKind
    {
        Floor,
        Box,
        Stair,
        Other,
        Player
    }

    class Record
    {
        public Transform transform;
        public TargetKind kind;
        public Box box;
        public Transform originalParent;
        public int siblingIndex;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public Vector3 worldPosition;
        public Bounds originalBounds;
        public Bounds silhouetteBounds;
        public Rigidbody rigidbody;
        public bool rigidbodyKinematic;
        public RigidbodyConstraints rigidbodyConstraints;
        public Transform scaleRoot;
        public bool deactivatedByOcclusion;
        public bool wasHeld;
        public readonly List<Collider> disabledColliders = new List<Collider>();
        public readonly List<Transform> depthScaledChildren = new List<Transform>();
        public readonly List<Vector3> depthScaledChildScales = new List<Vector3>();
    }

    readonly View view;
    readonly List<Record> records = new List<Record>();
    bool flattened;

    public ViewFlattenSystem(View view)
    {
        this.view = view;
    }

    public bool TryEnter2D(View.State state)
    {
        if (flattened)
        {
            return false;
        }

        CollectRecords(state);

        if (PlayerOverlapsBox(state))
        {
            records.Clear();
            Text warning = Object.FindObjectOfType<Text>();
            if (warning != null)
            {
                warning.PlayWrong();
            }
            return false;
        }

        Camera viewCamera = GetViewCamera(state);
        float plane = GetPlaneCoordinate(state);
        plane = ClampPlaneToCamera(viewCamera, state, plane);

        for (int i = 0; i < records.Count; i++)
        {
            FlattenRecord(records[i], state, plane, viewCamera);
        }

        Physics.SyncTransforms();
        ApplyOcclusion(state);
        flattened = true;
        return true;
    }

    public void RestoreFrom2D(View.State state)
    {
        if (!flattened)
        {
            return;
        }

        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.deactivatedByOcclusion)
            {
                GameObject hideObject = record.scaleRoot != null
                    ? record.scaleRoot.gameObject
                    : record.transform != null ? record.transform.gameObject : null;
                if (hideObject != null)
                {
                    hideObject.SetActive(true);
                }
            }
        }

        Physics.SyncTransforms();

        List<Record> movedBoxes = new List<Record>();

        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.transform == null)
            {
                continue;
            }

            RestoreDisabledColliders(record);

            if (record.kind == TargetKind.Player)
            {
                RestorePlayer(record, state);
                continue;
            }

            if (IsMovedBox(record, state))
            {
                movedBoxes.Add(record);
                continue;
            }

            RestoreExact(record);
        }

        Physics.SyncTransforms();

        for (int i = 0; i < movedBoxes.Count; i++)
        {
            RestoreMovedBox(movedBoxes[i], state);
        }

        view.GetComponent<ChangeTo3D>().SnapPlayerFrom2D(state);
        flattened = false;
        records.Clear();
    }

    void CollectRecords(View.State state)
    {
        records.Clear();
        List<Transform> targets = new List<Transform>();

        foreach (Floor floor in Floor.allFloors)
        {
            if (floor != null)
            {
                AddUnique(targets, ResolveGroupRoot(floor.transform));
            }
        }

        AddComponents(targets, Object.FindObjectsOfType<Box>());
        AddComponents(targets, Object.FindObjectsOfType<Key>());
        AddComponents(targets, Object.FindObjectsOfType<Door>());
        AddComponents(targets, Object.FindObjectsOfType<ItemGetAbility2DX>());
        AddComponents(targets, Object.FindObjectsOfType<ItemGetAbility2DZ>());
        AddUnique(targets, view.transform);

        ShadowPlatform[] shadowPlatforms = Object.FindObjectsOfType<ShadowPlatform>();
        for (int i = 0; i < shadowPlatforms.Length; i++)
        {
            ShadowPlatform shadowPlatform = shadowPlatforms[i];
            if (shadowPlatform != null && shadowPlatform.MatchesView(state))
            {
                AddUnique(targets, shadowPlatform.transform);
            }
        }

        FilterNestedTargets(targets);

        for (int i = 0; i < targets.Count; i++)
        {
            Transform target = targets[i];
            if (target == null)
            {
                continue;
            }

            Bounds silhouette = ViewWorldBounds.GetSilhouette(target);
            Record record = new Record
            {
                transform = target,
                originalParent = target.parent,
                siblingIndex = target.GetSiblingIndex(),
                localPosition = target.localPosition,
                localRotation = target.localRotation,
                localScale = target.localScale,
                worldPosition = target.position,
                originalBounds = silhouette,
                silhouetteBounds = silhouette,
                rigidbody = target.GetComponent<Rigidbody>(),
                box = target.GetComponent<Box>()
            };

            if (target == view.transform)
            {
                record.kind = TargetKind.Player;
            }
            else if (record.box != null)
            {
                record.kind = TargetKind.Box;
                record.wasHeld = record.box.IsHeld;
            }
            else if (target.name.IndexOf("stair", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                record.kind = TargetKind.Stair;
            }
            else if (target.GetComponent<Floor>() != null ||
                     target.GetComponentInChildren<Floor>() != null ||
                     target.GetComponent<ShadowPlatform>() != null)
            {
                record.kind = TargetKind.Floor;
            }
            else
            {
                record.kind = TargetKind.Other;
            }

            if (record.rigidbody != null)
            {
                record.rigidbodyKinematic = record.rigidbody.isKinematic;
                record.rigidbodyConstraints = record.rigidbody.constraints;
            }

            records.Add(record);
        }
    }

    static Transform ResolveGroupRoot(Transform target)
    {
        BalanceScale balance = target.GetComponentInParent<BalanceScale>();
        if (balance != null)
        {
            return balance.transform;
        }

        Transform parent = target.parent;
        if (parent == null)
        {
            return target;
        }

        if (parent.GetComponent<Floor>() != null ||
            parent.GetComponent<Box>() != null ||
            parent.GetComponent<Key>() != null ||
            parent.GetComponent<Door>() != null)
        {
            return target;
        }

        Floor[] floors = parent.GetComponentsInChildren<Floor>(true);
        if (floors.Length >= 2 && ParentLooksLikeSingleProp(parent))
        {
            return parent;
        }

        return target;
    }

    static bool ParentLooksLikeSingleProp(Transform parent)
    {
        string parentName = parent.name.ToLowerInvariant();
        if (parentName.Contains("stair") || parentName.Contains("step"))
        {
            return true;
        }

        Renderer[] renderers = parent.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (renderer.GetComponent<Floor>() == null)
            {
                return true;
            }
        }

        return false;
    }

    static void AddComponents<T>(List<Transform> targets, T[] components) where T : Component
    {
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null)
            {
                AddUnique(targets, components[i].transform);
            }
        }
    }

    static void AddUnique(List<Transform> targets, Transform target)
    {
        if (target != null && !targets.Contains(target))
        {
            targets.Add(target);
        }
    }

    static void FilterNestedTargets(List<Transform> targets)
    {
        for (int i = targets.Count - 1; i >= 0; i--)
        {
            Transform candidate = targets[i];
            for (int j = 0; j < targets.Count; j++)
            {
                if (i == j || targets[j] == null || candidate == null)
                {
                    continue;
                }

                if (candidate.IsChildOf(targets[j]))
                {
                    targets.RemoveAt(i);
                    break;
                }
            }
        }
    }

    bool PlayerOverlapsBox(View.State state)
    {
        Bounds playerBounds = ViewWorldBounds.Get(view.transform);
        Rect playerRect = ViewWorldBounds.ProjectToView(playerBounds, state);

        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            bool isBox = record.kind == TargetKind.Box && record.box != null && !record.box.IsHeld;
            bool isStair = record.kind == TargetKind.Stair;
            if (!isBox && !isStair)
            {
                continue;
            }

            Rect boxRect = ViewWorldBounds.ProjectToView(record.silhouetteBounds, state);
            if (ViewWorldBounds.OverlapRatio(boxRect, playerRect) >= view.occlusionOverlapRatio)
            {
                return true;
            }
        }

        return false;
    }

    void FlattenRecord(Record record, View.State state, float plane, Camera viewCamera)
    {
        Transform target = record.transform;
        Rigidbody body = record.rigidbody;
        bool playerBody = record.kind == TargetKind.Player;
        Vector3 preservedVelocity = Vector3.zero;
        if (body != null)
        {
            preservedVelocity = body.velocity;
            if (!playerBody)
            {
                if (!body.isKinematic)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = true;
            }
        }

        Bounds silhouette = record.silhouetteBounds;
        Vector3 rootCenter = record.kind == TargetKind.Player
            ? target.position
            : silhouette.center;
        Transform scaleRoot = InsertScaleRoot(target, rootCenter);
        record.scaleRoot = scaleRoot;

        Vector3 flattened = scaleRoot.position;
        if (state == View.State.Version2DX)
        {
            flattened.x = plane;
        }
        else
        {
            flattened.z = plane;
        }

        scaleRoot.position = flattened;
        if (playerBody && body != null)
        {
            Vector3 playerPosition = body.position;
            if (state == View.State.Version2DX)
            {
                playerPosition.x = plane;
            }
            else
            {
                playerPosition.z = plane;
            }

            body.position = playerPosition;
            target.position = playerPosition;
        }

        Physics.SyncTransforms();

        bool depthOnly = record.kind == TargetKind.Player;
        Vector3 worldScale = ComputeSilhouetteWorldScale(
            target,
            silhouette,
            state,
            plane,
            viewCamera,
            depthOnly);
        if (record.kind == TargetKind.Player)
        {
            scaleRoot.localScale = Vector3.one;
            ApplyDepthScaleToVisual(record, state, worldScale);
        }
        else
        {
            scaleRoot.localScale = worldScale;
        }

        Physics.SyncTransforms();

        bool keepOwnColliders =
            record.kind == TargetKind.Box ||
            record.kind == TargetKind.Player ||
            record.rigidbody != null ||
            target.GetComponent<BalanceScale>() != null;

        if (!keepOwnColliders)
        {
            DisableChildColliders(record);
            AddSilhouetteCollider(scaleRoot, silhouette, state, worldScale, record.kind == TargetKind.Floor);
        }

        if (body != null && (record.box == null || !record.box.IsHeld))
        {
            if (playerBody)
            {
                if (state == View.State.Version2DX)
                {
                    preservedVelocity.x = 0f;
                }
                else
                {
                    preservedVelocity.z = 0f;
                }

                body.useGravity = true;
                body.velocity = preservedVelocity;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
            else
            {
                body.isKinematic = record.rigidbodyKinematic;
                if (!body.isKinematic)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            if (state == View.State.Version2DX)
            {
                body.constraints =
                    RigidbodyConstraints.FreezePositionX |
                    RigidbodyConstraints.FreezeRotationX |
                    RigidbodyConstraints.FreezeRotationY |
                    RigidbodyConstraints.FreezeRotationZ;
            }
            else
            {
                body.constraints =
                    RigidbodyConstraints.FreezePositionZ |
                    RigidbodyConstraints.FreezeRotationX |
                    RigidbodyConstraints.FreezeRotationY |
                    RigidbodyConstraints.FreezeRotationZ;
            }
        }
    }

    static void ApplyDepthScaleToVisual(Record record, View.State state, Vector3 worldScale)
    {
        Transform player = record.transform;
        float depth = state == View.State.Version2DX ? worldScale.x : worldScale.z;
        for (int i = 0; i < player.childCount; i++)
        {
            Transform child = player.GetChild(i);
            if (child.GetComponent<Rigidbody>() != null || child.GetComponent<GroundChecker>() != null)
            {
                continue;
            }

            record.depthScaledChildren.Add(child);
            record.depthScaledChildScales.Add(child.localScale);

            Vector3 scale = child.localScale;
            if (state == View.State.Version2DX)
            {
                scale.x *= depth;
            }
            else
            {
                scale.z *= depth;
            }

            child.localScale = scale;
        }
    }

    static void RestoreDepthScaledChildren(Record record)
    {
        for (int i = 0; i < record.depthScaledChildren.Count; i++)
        {
            Transform child = record.depthScaledChildren[i];
            if (child != null)
            {
                child.localScale = record.depthScaledChildScales[i];
            }
        }
    }

    static Transform InsertScaleRoot(Transform target, Vector3 worldCenter)
    {
        GameObject rootObject = new GameObject(target.name + "_ViewScaleRoot");
        rootObject.AddComponent<ViewScaleRoot>();
        Transform root = rootObject.transform;
        Transform parent = target.parent;
        int siblingIndex = target.GetSiblingIndex();

        root.SetParent(parent, false);
        root.position = worldCenter;
        root.rotation = Quaternion.identity;
        root.localScale = Vector3.one;
        root.SetSiblingIndex(siblingIndex);
        target.SetParent(root, true);
        return root;
    }

    Vector3 ComputeSilhouetteWorldScale(
        Transform target,
        Bounds silhouette,
        View.State state,
        float plane,
        Camera viewCamera,
        bool depthOnly)
    {
        float thickness = Mathf.Max(0.05f, view.flattenThickness);
        thickness = ClampThicknessToCamera(viewCamera, state, plane, thickness);

        Physics.SyncTransforms();
        Bounds current = ViewWorldBounds.GetSilhouette(target);
        float currentWidth = state == View.State.Version2DX ? current.size.z : current.size.x;
        float currentHeight = Mathf.Max(0.0001f, current.size.y);
        float currentDepth = state == View.State.Version2DX
            ? Mathf.Max(0.0001f, current.size.x)
            : Mathf.Max(0.0001f, current.size.z);
        float targetWidth = state == View.State.Version2DX
            ? Mathf.Max(0.0001f, silhouette.size.z)
            : Mathf.Max(0.0001f, silhouette.size.x);
        float targetHeight = Mathf.Max(0.0001f, silhouette.size.y);

        float widthScale = depthOnly ? 1f : targetWidth / Mathf.Max(0.0001f, currentWidth);
        float heightScale = depthOnly ? 1f : targetHeight / currentHeight;
        float depthScale = thickness / currentDepth;

        if (state == View.State.Version2DX)
        {
            return new Vector3(depthScale, heightScale, widthScale);
        }

        return new Vector3(widthScale, heightScale, depthScale);
    }

    float ClampThicknessToCamera(
        Camera viewCamera,
        View.State state,
        float plane,
        float thickness)
    {
        if (viewCamera == null)
        {
            return thickness;
        }

        GetCameraAxisLimits(viewCamera, state, out float minAxis, out float maxAxis);
        float maxHalf = Mathf.Max(0.025f, Mathf.Min(plane - minAxis, maxAxis - plane));
        return Mathf.Min(thickness, maxHalf * 2f);
    }

    void DisableChildColliders(Record record)
    {
        record.disabledColliders.Clear();
        Collider[] colliders = record.transform.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            collider.enabled = false;
            record.disabledColliders.Add(collider);
        }
    }

    void AddSilhouetteCollider(
        Transform scaleRoot,
        Bounds silhouette,
        View.State state,
        Vector3 worldScale,
        bool walkableGround)
    {
        float thickness = Mathf.Max(0.05f, view.flattenThickness);
        BoxCollider wall = scaleRoot.gameObject.AddComponent<BoxCollider>();
        Vector3 rootScale = scaleRoot.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(rootScale.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(rootScale.y));
        float sz = Mathf.Max(0.0001f, Mathf.Abs(rootScale.z));

        if (state == View.State.Version2DX)
        {
            wall.size = new Vector3(
                thickness / sx,
                silhouette.size.y / sy,
                silhouette.size.z / sz);
        }
        else
        {
            wall.size = new Vector3(
                silhouette.size.x / sx,
                silhouette.size.y / sy,
                thickness / sz);
        }

        wall.center = Vector3.zero;
        if (walkableGround)
        {
            scaleRoot.gameObject.tag = "Ground";
        }
    }

    static void RestoreDisabledColliders(Record record)
    {
        for (int i = 0; i < record.disabledColliders.Count; i++)
        {
            if (record.disabledColliders[i] != null)
            {
                record.disabledColliders[i].enabled = true;
            }
        }

        record.disabledColliders.Clear();
    }

    float GetPlaneCoordinate(View.State state)
    {
        Vector3 playerPosition = view.transform.position;
        return state == View.State.Version2DX ? playerPosition.x : playerPosition.z;
    }

    float ClampPlaneToCamera(Camera viewCamera, View.State state, float plane)
    {
        if (viewCamera == null)
        {
            return plane;
        }

        GetCameraAxisLimits(viewCamera, state, out float minAxis, out float maxAxis);
        return Mathf.Clamp(plane, minAxis + 0.05f, maxAxis - 0.05f);
    }

    void GetCameraAxisLimits(Camera viewCamera, View.State state, out float minAxis, out float maxAxis)
    {
        float padding = Mathf.Max(0f, view.cameraDepthPadding);
        if (state == View.State.Version2DX)
        {
            Vector3 cameraPosition = Get2DXCameraPosition();
            minAxis = cameraPosition.x - viewCamera.farClipPlane + padding;
            maxAxis = cameraPosition.x - viewCamera.nearClipPlane - padding;
            return;
        }

        Vector3 camera2DZPosition = Get2DZCameraPosition();
        minAxis = camera2DZPosition.z + viewCamera.nearClipPlane + padding;
        maxAxis = camera2DZPosition.z + viewCamera.farClipPlane - padding;
    }

    Vector3 Get2DXCameraPosition()
    {
        float cameraX = LevelConfig.instance != null
            ? LevelConfig.instance.cameraPosition2DX
            : 500f;
        Vector3 playerPosition = view.transform.position;
        return new Vector3(cameraX, playerPosition.y + 1f, playerPosition.z);
    }

    Vector3 Get2DZCameraPosition()
    {
        float cameraZ = LevelConfig.instance != null
            ? LevelConfig.instance.cameraPosition2DZ
            : -500f;
        Vector3 playerPosition = view.transform.position;
        return new Vector3(playerPosition.x, playerPosition.y + 1f, cameraZ);
    }

    Camera GetViewCamera(View.State state)
    {
        CameraController cameraController = Object.FindObjectOfType<CameraController>();
        if (cameraController == null)
        {
            return null;
        }

        if (state == View.State.Version2DX)
        {
            return cameraController.camera2DX != null
                ? cameraController.camera2DX.GetComponent<Camera>()
                : null;
        }

        return cameraController.camera2DZ != null
            ? cameraController.camera2DZ.GetComponent<Camera>()
            : null;
    }

    void ApplyOcclusion(View.State state)
    {
        for (int i = 0; i < records.Count; i++)
        {
            Record target = records[i];
            if (target.transform == null ||
                target.kind == TargetKind.Player ||
                target.transform.GetComponent<ShadowPlatform>() != null)
            {
                continue;
            }

            Rect targetRect = ViewWorldBounds.ProjectToView(target.silhouetteBounds, state);
            float targetDepth = ViewWorldBounds.CloserDepthScore(target.silhouetteBounds, state);
            bool occluded = false;

            for (int j = 0; j < records.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                Record occluder = records[j];
                if (occluder.transform == null)
                {
                    continue;
                }

                if (occluder.kind == TargetKind.Player)
                {
                    continue;
                }

                if (occluder.kind == TargetKind.Box && occluder.box != null && occluder.box.IsHeld)
                {
                    continue;
                }

                float occluderDepth = ViewWorldBounds.CloserDepthScore(occluder.silhouetteBounds, state);
                if (occluderDepth <= targetDepth + DepthEpsilon)
                {
                    continue;
                }

                Rect occluderRect = ViewWorldBounds.ProjectToView(occluder.silhouetteBounds, state);
                if (ViewWorldBounds.OverlapRatio(occluderRect, targetRect) >= view.occlusionOverlapRatio)
                {
                    occluded = true;
                    break;
                }
            }

            if (occluded)
            {
                target.deactivatedByOcclusion = true;
                GameObject hideObject = target.scaleRoot != null
                    ? target.scaleRoot.gameObject
                    : target.transform.gameObject;
                hideObject.SetActive(false);
            }
        }
    }

    bool IsMovedBox(Record record, View.State state)
    {
        if (record.kind != TargetKind.Box || record.box == null || record.transform == null)
        {
            return false;
        }

        if (record.box.IsHeld)
        {
            return false;
        }

        Vector3 current = record.transform.position;
        if (state == View.State.Version2DX)
        {
            return Mathf.Abs(current.y - record.worldPosition.y) > MovedEpsilon ||
                   Mathf.Abs(current.z - record.worldPosition.z) > MovedEpsilon;
        }

        return Mathf.Abs(current.x - record.worldPosition.x) > MovedEpsilon ||
               Mathf.Abs(current.y - record.worldPosition.y) > MovedEpsilon;
    }

    void RestoreExact(Record record)
    {
        Vector3 currentWorld = record.transform.position;
        Quaternion currentWorldRotation = record.transform.rotation;
        TearDownScaleRoot(record);

        if (record.box != null && record.box.IsHeld)
        {
            record.transform.localScale = record.localScale;
            record.transform.position = currentWorld;
            record.transform.rotation = Quaternion.identity;
            RestoreRigidbody(record, keepHeld: true);
            if (record.rigidbody != null)
            {
                record.rigidbody.position = currentWorld;
                record.rigidbody.rotation = Quaternion.identity;
            }

            return;
        }

        record.transform.SetParent(record.originalParent, false);
        if (record.originalParent != null)
        {
            int siblingIndex = Mathf.Clamp(
                record.siblingIndex,
                0,
                Mathf.Max(0, record.originalParent.childCount - 1));
            record.transform.SetSiblingIndex(siblingIndex);
        }

        record.transform.localPosition = record.localPosition;
        record.transform.localRotation = record.localRotation;
        record.transform.localScale = record.localScale;
        RestoreRigidbody(record, keepHeld: false);
        if (record.rigidbody != null)
        {
            record.rigidbody.position = record.transform.position;
            record.rigidbody.rotation = record.transform.rotation;
        }
    }

    void RestorePlayer(Record record, View.State state)
    {
        Vector3 current = record.transform.position;
        RestoreDepthScaledChildren(record);
        TearDownScaleRoot(record);

        record.transform.SetParent(record.originalParent, true);
        record.transform.localRotation = record.localRotation;
        record.transform.localScale = record.localScale;

        Vector3 restored = current;
        if (state == View.State.Version2DX)
        {
            restored.x = ChangeTo3D.FindFloorAxisFrom2DX(current);
            restored.y = current.y;
            restored.z = current.z;
        }
        else
        {
            restored.z = ChangeTo3D.FindFloorAxisFrom2DZ(current);
            restored.x = current.x;
            restored.y = current.y;
        }

        if (record.rigidbody != null)
        {
            record.rigidbody.position = restored;
        }

        record.transform.position = restored;
        RestoreRigidbody(record, keepHeld: false);
    }

    void RestoreMovedBox(Record record, View.State state)
    {
        Vector3 current = record.transform.position;
        TearDownScaleRoot(record);

        record.transform.SetParent(record.originalParent, true);
        record.transform.localRotation = record.localRotation;
        record.transform.localScale = record.localScale;

        Vector3 restored = current;
        if (state == View.State.Version2DX)
        {
            restored.x = ChangeTo3D.FindFloorAxisFrom2DX(current);
            restored.y = current.y;
            restored.z = current.z;
        }
        else
        {
            restored.z = ChangeTo3D.FindFloorAxisFrom2DZ(current);
            restored.x = current.x;
            restored.y = current.y;
        }

        if (record.rigidbody != null)
        {
            record.rigidbody.position = restored;
        }

        record.transform.position = restored;
        RestoreRigidbody(record, keepHeld: false);
    }

    static void TearDownScaleRoot(Record record)
    {
        if (record.scaleRoot == null)
        {
            return;
        }

        Transform root = record.scaleRoot;
        if (record.transform.parent == root)
        {
            record.transform.SetParent(root.parent, true);
        }

        Object.Destroy(root.gameObject);
        record.scaleRoot = null;
    }

    void RestoreRigidbody(Record record, bool keepHeld)
    {
        if (record.rigidbody == null)
        {
            return;
        }

        record.rigidbody.constraints = record.rigidbodyConstraints;
        if (keepHeld)
        {
            record.rigidbody.isKinematic = true;
            return;
        }

        record.rigidbody.isKinematic = record.rigidbodyKinematic;
        if (!record.rigidbody.isKinematic)
        {
            record.rigidbody.velocity = Vector3.zero;
            record.rigidbody.angularVelocity = Vector3.zero;
        }
    }
}
