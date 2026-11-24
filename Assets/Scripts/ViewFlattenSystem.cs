using System.Collections.Generic;
using UnityEngine;

public class ViewFlattenSystem
{
    const float DepthEpsilon = 0.01f;
    // 高出脚底这么多的面当成头顶，不能拿来当落点。
    const float OverheadReject = 0.35f;
    enum TargetKind
    {
        Floor,
        Box,
        Stair,
        Other,
        Player
    }

    // 角色模型会跟着移动转向。压扁要留在世界坐标里，不能写进模型的本地轴。
    class VisualPose
    {
        public Transform transform;
        public int siblingIndex;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
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
        public Bounds supportBounds;
        public Rigidbody rigidbody;
        public bool rigidbodyKinematic;
        public RigidbodyConstraints rigidbodyConstraints;
        public Transform scaleRoot;
        public bool deactivatedByOcclusion;
        public bool wasHeld;
        public readonly List<Collider> disabledColliders = new List<Collider>();
        public readonly List<Transform> depthScaledChildren = new List<Transform>();
        public readonly List<Vector3> depthScaledChildScales = new List<Vector3>();
        public Vector3 flatWorldScale = Vector3.one;
        public Vector3 progressFromScale = Vector3.one;
        public Vector3 progressToScale = Vector3.one;
        public float progressFromDepth;
        public float progressToDepth;
        public bool driveDepth;
        public readonly List<Vector3> childScaleFrom = new List<Vector3>();
        public readonly List<Vector3> childScaleTo = new List<Vector3>();
        public Transform visualFlattenRoot;
        public Vector3 perspectiveBaseVisualScale = Vector3.one;
        public bool hasPerspectiveBase;
        public bool stairTreadsInstalled;
        public Transform dropBodyRoot;
        public bool holdAnchor;
        public Vector3 anchorWorld;
        public Vector3 anchorLocal;
        public readonly List<VisualPose> visualPoses = new List<VisualPose>();
    }

    // 进入 2D 之前的可站立表面。退出时地面已经被压扁，不能再读当前位置。
    class RestoredSurface
    {
        public Bounds bounds;
        public float walkTop;
        public bool shadow;
        public ShadowPlatform platform;
    }

    readonly View view;
    readonly List<Record> records = new List<Record>();
    readonly List<RestoredSurface> restoredSurfaces = new List<RestoredSurface>();
    bool flattened;
    bool visualAnimating;
    bool animatingEnter;
    bool holdPlayerOnStair;
    Vector3 pinnedPlayerPosition;
    View.State animState;

    public ViewFlattenSystem(View view)
    {
        this.view = view;
    }

    // 只搭好缩放父节点并记下起点、终点。外形先不变。
    // 人和箱子在投影上重叠时直接失败，镜头也不会飞。
    public bool PrepareEnter(View.State state)
    {
        holdPlayerOnStair = false;
        if (flattened || visualAnimating)
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

        CaptureRestoredSurfaces();

        Camera viewCamera = GetViewCamera(state);
        float plane = GetPlaneCoordinate(state);
        plane = ClampPlaneToCamera(viewCamera, state, plane);
        animState = state;
        animatingEnter = true;
        visualAnimating = true;
        RememberStairStep();

        for (int i = 0; i < records.Count; i++)
        {
            PrepareEnterRecord(records[i], state, plane, viewCamera);
        }

        return true;
    }

    // 从已经压扁的状态往回放。起点是现在的扁外形，终点是立体外形。
    // 镜头起飞前先把角色放到恢复后的地面上。厚度仍和所有地面一起，跟着镜头变。
    public bool PrepareExit(View.State state)
    {
        holdPlayerOnStair = false;
        if (!flattened || visualAnimating)
        {
            return false;
        }

        ReleaseAllDropBodies();
        animState = state;
        animatingEnter = false;
        visualAnimating = true;

        for (int i = 0; i < records.Count; i++)
        {
            PrepareExitRecord(records[i], state);
        }

        PlacePlayerOnRestoredGround(state);
        return true;
    }

    // t = 0 是这次飞行开始时的外形，t = 1 是飞完后的外形。
    public void SetVisualProgress(float t)
    {
        if (!visualAnimating)
        {
            return;
        }

        t = Mathf.Clamp01(t);
        for (int i = 0; i < records.Count; i++)
        {
            ApplyRecordVisual(records[i], t);
        }

        KeepPinnedPlayer();
        Physics.SyncTransforms();
    }

    // 压扁父节点不跟着角色转。角色一转向，这里把模型的朝向补回去，平面仍正对镜头。
    public void SyncPlayerVisual()
    {
        if (!visualAnimating && !flattened)
        {
            return;
        }

        if (flattened && !visualAnimating)
        {
            RefreshPerspectiveVisual();
        }

        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.kind != TargetKind.Player ||
                record.visualFlattenRoot == null ||
                record.transform == null)
            {
                continue;
            }

            Transform player = record.transform;
            Transform wrapper = record.visualFlattenRoot;
            wrapper.rotation = Quaternion.identity;
            wrapper.position = player.position;

            for (int j = 0; j < record.visualPoses.Count; j++)
            {
                VisualPose pose = record.visualPoses[j];
                if (pose.transform == null)
                {
                    continue;
                }

                pose.transform.rotation = player.rotation * pose.localRotation;
                pose.transform.position = player.TransformPoint(pose.localPosition);
            }
        }
    }

    // 镜头到位后调用。进入 2D 时补上碰撞、遮挡和约束；离开时拆掉压扁结构。
    public void CommitActiveTransition()
    {
        if (animatingEnter)
        {
            SetVisualProgress(1f);
            FinishEnterGameplay();
            holdPlayerOnStair = false;
            visualAnimating = false;
            animatingEnter = false;
            // 模型先压到最终厚度。人在触发区域内才按边界定一次倍数，最后把脚底贴回地面。
            ApplyEnteredPerspectiveScale();
            StickPlayerToGround();
            return;
        }

        if (!visualAnimating)
        {
            return;
        }

        SetVisualProgress(1f);
        visualAnimating = false;
        if (flattened)
        {
            RestoreFrom2D(animState);
        }

        StickPlayerToGround();
    }

    // 进入 2D 时定下的倍数保持不变。模型厚度保持压扁结果，只乘这一个倍数。
    void ApplyEnteredPerspectiveScale()
    {
        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.kind != TargetKind.Player || record.visualFlattenRoot == null)
            {
                continue;
            }

            record.perspectiveBaseVisualScale = record.visualFlattenRoot.localScale;
            record.hasPerspectiveBase = true;
        }

        RefreshPerspectiveVisual();
    }

    void RefreshPerspectiveVisual()
    {
        CharacterPerspectiveScaler scaler = view.GetComponent<CharacterPerspectiveScaler>();
        if (scaler == null || !scaler.TryApplyForView(animState))
        {
            return;
        }

        float multiplier = scaler.CurrentMultiplier;
        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.kind != TargetKind.Player ||
                record.visualFlattenRoot == null ||
                !record.hasPerspectiveBase)
            {
                continue;
            }

            record.visualFlattenRoot.localScale = record.perspectiveBaseVisualScale * multiplier;
        }
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
                RestorePlayer(record);
                continue;
            }

            RestoreExact(record);
        }

        Physics.SyncTransforms();

        // 落点用进 2D 之前记下的表面。箱子回到自己原来的位置，人站在上面就留在箱顶。
        ApplySavedPlayerLanding(state);
        CharacterPerspectiveScaler scaler = view.GetComponent<CharacterPerspectiveScaler>();
        if (scaler != null)
        {
            scaler.RestoreOriginalSize();
        }

        flattened = false;
        records.Clear();
        restoredSurfaces.Clear();
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
        AddComponents(targets, Object.FindObjectsOfType<PuzzleItem>());
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
                supportBounds = ViewWorldBounds.Get(target),
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
            if (ViewWorldBounds.OverlapRatio(boxRect, playerRect) < view.occlusionOverlapRatio)
            {
                continue;
            }

            // 站在楼梯厚度里允许切换。整段楼梯都比人更靠近镜头，才算被挡住。
            if (isStair && !StairIsInFrontOfPlayer(record.silhouetteBounds, playerBounds, state))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    // 2DX 镜头在 +X 朝 -X 看，更大的 X 更近。2DZ 镜头在 -Z 朝 +Z 看，更小的 Z 更近。
    static bool StairIsInFrontOfPlayer(Bounds stair, Bounds player, View.State state)
    {
        if (state == View.State.Version2DX)
        {
            return stair.min.x > player.max.x;
        }

        return stair.max.z < player.min.z;
    }

    void PrepareEnterRecord(Record record, View.State state, float plane, Camera viewCamera)
    {
        Transform target = record.transform;
        if (target == null)
        {
            return;
        }

        bool playerBody = record.kind == TargetKind.Player;
        // 别的刚体先停掉物理，否则缩放父节点一动，模拟会把物体拽回去。
        // 玩家保持动态，飞行过程中仍能按 3D 方式走路。
        if (record.rigidbody != null && !playerBody)
        {
            if (!record.rigidbody.isKinematic)
            {
                record.rigidbody.velocity = Vector3.zero;
                record.rigidbody.angularVelocity = Vector3.zero;
            }

            record.rigidbody.isKinematic = true;
        }

        Bounds silhouette = record.silhouetteBounds;
        Vector3 rootCenter = playerBody ? target.position : silhouette.center;
        Transform scaleRoot = InsertScaleRoot(target, rootCenter);
        record.scaleRoot = scaleRoot;

        bool depthOnly = playerBody;
        Vector3 worldScale = ComputeSilhouetteWorldScale(
            target,
            silhouette,
            state,
            plane,
            viewCamera,
            depthOnly);
        record.flatWorldScale = worldScale;
        record.progressFromScale = scaleRoot.localScale;
        record.progressToScale = playerBody ? Vector3.one : worldScale;
        record.progressFromDepth = AxisOf(target.position, state);
        record.progressToDepth = plane;
        // 玩家本来就站在压扁平面上，进入时不挪他的位置，只把子网格压薄。
        record.driveDepth = !playerBody;

        if (record.kind == TargetKind.Stair)
        {
            // 压扁绕着包围盒中心收。楼梯沿被压扁的轴展开时，上一级会滑向中心。
            // 人站在哪一级，就把这一级上的点钉在原地，其余部分朝人收拢。
            if (holdPlayerOnStair && PlayerStandsInside(target))
            {
                record.holdAnchor = true;
                record.anchorWorld = pinnedPlayerPosition;
                record.anchorLocal = scaleRoot.InverseTransformPoint(record.anchorWorld);
            }

            // 镜头还没飞。只留每一级露在外面的踏面，被上一级盖住的实心体积先拆掉。
            List<Bounds> treads = CaptureExposedStairTreads(target);
            record.stairTreadsInstalled = AddStairStepColliders(scaleRoot, treads);
            if (record.stairTreadsInstalled)
            {
                DisableChildColliders(record);
                Physics.SyncTransforms();
            }
        }

        if (playerBody)
        {
            scaleRoot.localScale = Vector3.one;
            CapturePlayerDepthScales(record, state, worldScale);
            // 地面碰撞跟着缩放时会把人顶起来。先锁住高度，落地后再贴回地面。
            HoldPlayerHeight(record.rigidbody);
            if (holdPlayerOnStair)
            {
                LockPlayerOnStair(record.rigidbody);
            }
        }
    }

    void PrepareExitRecord(Record record, View.State state)
    {
        if (record.transform == null || record.scaleRoot == null)
        {
            record.driveDepth = false;
            return;
        }

        bool playerBody = record.kind == TargetKind.Player;
        bool held = record.box != null && record.box.IsHeld;
        record.progressFromScale = record.scaleRoot.localScale;
        record.progressToScale = Vector3.one;
        record.progressFromDepth = AxisOf(record.transform.position, state);
        if (playerBody)
        {
            Vector3 probe = record.transform.position;
            float referenceY = probe.y + GetFootOffset(record.transform);
            TryGetRestoredDepth(state, probe, referenceY, out float depth);
            record.progressToDepth = depth;
        }
        else
        {
            // 箱子和地面都回到进侧面之前的位置，不按平台中心重算。
            record.progressToDepth = AxisOf(record.worldPosition, state);
        }

        // 拿在手里的箱子跟着人，不单独沿深度滑回去。
        record.driveDepth = !held;

        record.childScaleFrom.Clear();
        record.childScaleTo.Clear();
        int count = Mathf.Min(
            record.depthScaledChildren.Count,
            record.depthScaledChildScales.Count);
        for (int i = 0; i < count; i++)
        {
            Transform child = record.depthScaledChildren[i];
            Vector3 from = child != null
                ? child.localScale
                : record.depthScaledChildScales[i];
            record.childScaleFrom.Add(from);
            Vector3 toScale = record.depthScaledChildScales[i];
            if (playerBody)
            {
                CharacterPerspectiveScaler scaler =
                    record.transform.GetComponent<CharacterPerspectiveScaler>();
                if (scaler != null)
                {
                    toScale *= scaler.CurrentMultiplier;
                }
            }

            record.childScaleTo.Add(toScale);
        }

        if (record.rigidbody == null)
        {
            return;
        }

        if (!playerBody)
        {
            if (!record.rigidbody.isKinematic)
            {
                record.rigidbody.velocity = Vector3.zero;
                record.rigidbody.angularVelocity = Vector3.zero;
            }

            record.rigidbody.isKinematic = true;
            return;
        }

        // 深度会在镜头起飞前写到恢复后的地面上，所以这条轴先不要锁死。
        // 高度继续锁住，避免地面变回立体时把人顶离表面。
        record.rigidbody.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;
        HoldPlayerHeight(record.rigidbody);
    }

    void ApplyRecordVisual(Record record, float t)
    {
        if (record.scaleRoot != null)
        {
            record.scaleRoot.localScale = Vector3.Lerp(
                record.progressFromScale,
                record.progressToScale,
                t);
        }

        int count = Mathf.Min(record.childScaleFrom.Count, record.childScaleTo.Count);
        count = Mathf.Min(count, record.depthScaledChildren.Count);
        for (int i = 0; i < count; i++)
        {
            Transform child = record.depthScaledChildren[i];
            if (child == null)
            {
                continue;
            }

            child.localScale = Vector3.Lerp(
                record.childScaleFrom[i],
                record.childScaleTo[i],
                t);
        }

        if (record.driveDepth && record.transform != null)
        {
            float depth = Mathf.Lerp(record.progressFromDepth, record.progressToDepth, t);
            MoveChildToDepth(record, depth);
        }

        // 深度补完之后再钉一次。否则楼梯中心仍会把人所站的那一级带走。
        KeepScaleAnchor(record);
    }

    // 先改缩放，再把物体上的深度轴补到目标值。缩放会带着子物体挪一点，所以深度要后写。
    void MoveChildToDepth(Record record, float depth)
    {
        if (record.scaleRoot == null || record.transform == null)
        {
            return;
        }

        float current = AxisOf(record.transform.position, animState);
        float delta = depth - current;
        if (Mathf.Abs(delta) > 0.0000001f)
        {
            float rootDepth = AxisOf(record.scaleRoot.position, animState) + delta;
            record.scaleRoot.position = WithAxis(record.scaleRoot.position, animState, rootDepth);
        }

        if (record.rigidbody == null)
        {
            return;
        }

        record.rigidbody.position = record.transform.position;
        if (record.rigidbody.isKinematic)
        {
            return;
        }

        Vector3 velocity = record.rigidbody.velocity;
        velocity = WithAxis(velocity, animState, 0f);
        record.rigidbody.velocity = velocity;
    }

    void FinishEnterGameplay()
    {
        for (int i = 0; i < records.Count; i++)
        {
            ApplyEnterGameplay(records[i]);
        }

        Physics.SyncTransforms();
        ApplyOcclusion(animState);
        Physics.SyncTransforms();
        flattened = true;
    }

    void ApplyEnterGameplay(Record record)
    {
        Transform target = record.transform;
        if (target == null || record.scaleRoot == null)
        {
            return;
        }

        Rigidbody body = record.rigidbody;
        bool playerBody = record.kind == TargetKind.Player;
        View.State state = animState;

        if (playerBody && body != null)
        {
            Vector3 playerPosition = WithAxis(body.position, state, record.progressToDepth);
            body.position = playerPosition;
            target.position = playerPosition;
        }

        bool keepOwnColliders =
            record.kind == TargetKind.Box ||
            record.kind == TargetKind.Player ||
            record.rigidbody != null ||
            target.GetComponent<BalanceScale>() != null ||
            target.GetComponent<Key>() != null ||
            target.GetComponent<PuzzleItem>() != null;

        if (!keepOwnColliders)
        {
            if (record.kind == TargetKind.Stair)
            {
                // 踏面在镜头起飞前就已经换上。这里再补一次，避免中途没换成踏面时退回整块空气墙。
                if (!record.stairTreadsInstalled)
                {
                    Physics.SyncTransforms();
                    List<Bounds> steps = CaptureExposedStairTreads(target);
                    DisableChildColliders(record);
                    record.stairTreadsInstalled = AddStairStepColliders(record.scaleRoot, steps);
                    if (!record.stairTreadsInstalled)
                    {
                        AddSilhouetteCollider(
                            record.scaleRoot,
                            record.silhouetteBounds,
                            state,
                            record.flatWorldScale,
                            true);
                    }
                }
            }
            else
            {
                // 外形包围盒含草丛等装饰，比真实碰撞高。站立高度要用原碰撞的上下沿。
                bool hasColliderHeight = TryGetSolidColliderHeight(target, out float minY, out float maxY);
                DisableChildColliders(record);
                AddSilhouetteCollider(
                    record.scaleRoot,
                    record.silhouetteBounds,
                    state,
                    record.flatWorldScale,
                    record.kind == TargetKind.Floor);
                if (hasColliderHeight)
                {
                    FitColliderHeight(record.scaleRoot, minY, maxY);
                }
            }
        }

        if (body == null || (record.box != null && record.box.IsHeld))
        {
            return;
        }

        if (playerBody)
        {
            Vector3 velocity = WithAxis(body.velocity, state, 0f);
            if (holdPlayerOnStair)
            {
                velocity = WithWalk(velocity, state, 0f);
            }

            body.useGravity = true;
            body.isKinematic = false;
            body.velocity = velocity;
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

    void RememberStairStep()
    {
        holdPlayerOnStair = false;
        Transform player = view.transform;
        if (player == null)
        {
            return;
        }

        float footY = player.position.y + GetFootOffset(player);
        Vector3 origin = player.position;
        origin.y = footY + 0.4f;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            1.2f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null || !BelongsToStair(hits[i].collider.transform))
            {
                continue;
            }

            if (hits[i].distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hits[i].distance;
            found = true;
        }

        if (!found)
        {
            Vector3 feet = player.position;
            feet.y = footY + 0.05f;
            Collider[] overlaps = Physics.OverlapSphere(
                feet,
                0.35f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] != null && BelongsToStair(overlaps[i].transform))
                {
                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            return;
        }

        pinnedPlayerPosition = player.position;
        holdPlayerOnStair = true;
    }

    bool PlayerStandsInside(Transform target)
    {
        if (target == null || view.transform == null)
        {
            return false;
        }

        Bounds bounds = ViewWorldBounds.Get(target);
        Vector3 position = view.transform.position;
        float footY = position.y + GetFootOffset(view.transform);
        return position.x >= bounds.min.x - 0.3f &&
               position.x <= bounds.max.x + 0.3f &&
               position.z >= bounds.min.z - 0.3f &&
               position.z <= bounds.max.z + 0.3f &&
               footY >= bounds.min.y - 0.4f &&
               footY <= bounds.max.y + 0.6f;
    }

    void KeepScaleAnchor(Record record)
    {
        if (!record.holdAnchor || record.scaleRoot == null)
        {
            return;
        }

        Vector3 current = record.scaleRoot.TransformPoint(record.anchorLocal);
        Vector3 correction = record.anchorWorld - current;
        if (correction.sqrMagnitude <= 0.0000001f)
        {
            return;
        }

        record.scaleRoot.position += correction;
    }

    void KeepPinnedPlayer()
    {
        if (!holdPlayerOnStair || !animatingEnter)
        {
            return;
        }

        Transform player = view.transform;
        if (player == null)
        {
            return;
        }

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = pinnedPlayerPosition;
            body.velocity = Vector3.zero;
        }

        player.position = pinnedPlayerPosition;
    }

    static bool BelongsToStair(Transform source)
    {
        Transform current = source;
        while (current != null)
        {
            if (current.name.IndexOf("stair", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    void LockPlayerOnStair(Rigidbody body)
    {
        if (body == null)
        {
            return;
        }

        body.constraints |=
            RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezePositionZ;
        body.velocity = Vector3.zero;
        body.position = pinnedPlayerPosition;
    }

    static void HoldPlayerHeight(Rigidbody body)
    {
        if (body == null)
        {
            return;
        }

        body.constraints |= RigidbodyConstraints.FreezePositionY;
        Vector3 velocity = body.velocity;
        velocity.y = 0f;
        body.velocity = velocity;
    }

    // 镜头还没动。角色先站到恢复后的地面上，地面也先回到原来的深度。
    // 厚度留到后面，和镜头用同一步从扁变回立体。
    void PlacePlayerOnRestoredGround(View.State state)
    {
        Record playerRecord = null;
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].kind == TargetKind.Player)
            {
                playerRecord = records[i];
                break;
            }
        }

        // 先看脚底下是不是箱子。箱子随后会回到原位，人要按记下的箱顶落下去。
        bool onBox = false;
        Record standingBox = null;
        float walkOffset = 0f;
        if (playerRecord != null && playerRecord.transform != null)
        {
            Vector3 probe = playerRecord.transform.position;
            float footY = probe.y + GetFootOffset(playerRecord.transform);
            onBox = TryFindBoxUnderFeet(state, probe, footY, out standingBox, out walkOffset);
        }

        LockRestoredDepths(state);

        if (playerRecord == null || playerRecord.transform == null)
        {
            Physics.SyncTransforms();
            return;
        }

        Transform player = playerRecord.transform;
        float footOffset = GetFootOffset(player);
        Vector3 standing = onBox && standingBox != null
            ? LandingOnSavedBox(standingBox, state, walkOffset, footOffset)
            : RestoredStandingPosition(player, state, player.position);
        float depth = AxisOf(standing, state);

        Rigidbody body = playerRecord.rigidbody;
        if (body != null)
        {
            body.position = standing;
            Vector3 velocity = body.velocity;
            velocity.y = 0f;
            body.velocity = velocity;
        }

        player.position = standing;
        playerRecord.progressFromDepth = depth;
        playerRecord.progressToDepth = depth;
        playerRecord.driveDepth = true;
        MoveChildToDepth(playerRecord, depth);
        HoldPlayerHeight(body);
        Physics.SyncTransforms();
    }

    void LockRestoredDepths(View.State state)
    {
        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.kind == TargetKind.Player || !record.driveDepth)
            {
                continue;
            }

            if (record.kind == TargetKind.Box && record.box != null && !record.box.IsHeld)
            {
                SnapBoxToSavedPosition(record, state);
                continue;
            }

            record.progressFromDepth = record.progressToDepth;
            MoveChildToDepth(record, record.progressToDepth);
        }
    }

    void CaptureRestoredSurfaces()
    {
        restoredSurfaces.Clear();
        foreach (Floor floor in Floor.allFloors)
        {
            if (floor == null || !floor.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (floor.name.IndexOf("stair", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                AddStairRestoredSurfaces(floor.transform))
            {
                continue;
            }

            Bounds bounds = ViewWorldBounds.Get(floor.transform);
            restoredSurfaces.Add(new RestoredSurface
            {
                bounds = bounds,
                walkTop = SolidWalkTop(floor.transform, bounds),
                shadow = false
            });
        }

        ShadowPlatform[] platforms = Object.FindObjectsOfType<ShadowPlatform>();
        for (int i = 0; i < platforms.Length; i++)
        {
            ShadowPlatform platform = platforms[i];
            if (platform == null || !platform.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = ViewWorldBounds.Get(platform.transform);
            restoredSurfaces.Add(new RestoredSurface
            {
                bounds = bounds,
                walkTop = SolidWalkTop(platform.transform, bounds),
                shadow = true,
                platform = platform
            });
        }
    }

    bool TryGetRestoredDepth(View.State state, Vector3 worldPosition, out float depth)
    {
        return TryGetRestoredDepth(state, worldPosition, worldPosition.y, out depth);
    }

    // 在包住走位那条轴的表面里，选离参考高度最近的一块，用它进 2D 之前的中心轴。
    // 列表里靠后的一块不能把脚下这块盖掉。碰撞体被压到同一条左右线上时，也不能读那个中心。
    bool TryGetRestoredDepth(
        View.State state,
        Vector3 worldPosition,
        float referenceY,
        out float depth)
    {
        depth = AxisOf(worldPosition, state);
        bool found = false;
        float bestDistance = float.MaxValue;
        bool haveFallback = false;
        float fallbackDistance = float.MaxValue;
        float fallbackDepth = depth;

        for (int i = 0; i < restoredSurfaces.Count; i++)
        {
            RestoredSurface surface = restoredSurfaces[i];
            if (!MatchesExitView(surface, state) ||
                !CoversWalkAxis(surface.bounds, state, worldPosition))
            {
                continue;
            }

            float top = surface.walkTop;
            float candidate = state == View.State.Version2DX
                ? surface.bounds.center.x
                : surface.bounds.center.z;
            float distance = Mathf.Abs(referenceY - top);
            if (!haveFallback || distance < fallbackDistance)
            {
                haveFallback = true;
                fallbackDistance = distance;
                fallbackDepth = candidate;
            }

            if (top > referenceY + OverheadReject)
            {
                continue;
            }

            if (found && distance >= bestDistance)
            {
                continue;
            }

            found = true;
            bestDistance = distance;
            depth = candidate;
        }

        if (!found && haveFallback)
        {
            depth = fallbackDepth;
            return true;
        }

        return found;
    }

    static bool CoversWalkAxis(Bounds bounds, View.State state, Vector3 worldPosition)
    {
        if (state == View.State.Version2DX)
        {
            return worldPosition.z >= bounds.min.z && worldPosition.z <= bounds.max.z;
        }

        return worldPosition.x >= bounds.min.x && worldPosition.x <= bounds.max.x;
    }

    // 脚在箱子顶上时，箱子比平台更近。用现在的前后范围判断，落点用进侧面之前记下的箱子。
    bool TryFindBoxUnderFeet(
        View.State state,
        Vector3 playerPosition,
        float footY,
        out Record box,
        out float walkOffset)
    {
        box = null;
        walkOffset = 0f;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < restoredSurfaces.Count; i++)
        {
            RestoredSurface surface = restoredSurfaces[i];
            if (!MatchesExitView(surface, state) ||
                !CoversWalkAxis(surface.bounds, state, playerPosition))
            {
                continue;
            }

            float top = surface.walkTop;
            if (top > footY + OverheadReject)
            {
                continue;
            }

            float distance = Mathf.Abs(footY - top);
            if (distance < bestDistance)
            {
                bestDistance = distance;
            }
        }

        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.kind != TargetKind.Box || record.box == null || record.box.IsHeld)
            {
                continue;
            }

            Bounds current = CurrentBoxBounds(record);
            if (!CoversWalkAxis(current, state, playerPosition))
            {
                continue;
            }

            float top = current.max.y;
            if (top > footY + OverheadReject)
            {
                continue;
            }

            float distance = Mathf.Abs(footY - top);
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            box = record;
            walkOffset = WalkOf(playerPosition, state) - WalkOf(current.center, state);
        }

        return box != null;
    }

    static Bounds CurrentBoxBounds(Record record)
    {
        Bounds bounds = record.supportBounds;
        if (record.transform != null)
        {
            bounds.center += record.transform.position - record.worldPosition;
        }

        return bounds;
    }

    Vector3 LandingOnSavedBox(Record box, View.State state, float walkOffset, float footOffset)
    {
        Bounds saved = box.supportBounds;
        float walk = WalkOf(saved.center, state) + walkOffset;
        if (state == View.State.Version2DX)
        {
            walk = Mathf.Clamp(walk, saved.min.z, saved.max.z);
        }
        else
        {
            walk = Mathf.Clamp(walk, saved.min.x, saved.max.x);
        }

        Vector3 standing = saved.center;
        standing.y = saved.max.y - footOffset;
        return WithWalk(standing, state, walk);
    }

    // 视角切换不改箱子的位置，回到进侧面之前记下的坐标。
    void SnapBoxToSavedPosition(Record record, View.State state)
    {
        if (record.scaleRoot == null || record.transform == null)
        {
            return;
        }

        Vector3 delta = record.worldPosition - record.transform.position;
        record.scaleRoot.position += delta;
        if (record.rigidbody != null)
        {
            record.rigidbody.position = record.transform.position;
        }

        float depth = AxisOf(record.worldPosition, state);
        record.progressFromDepth = depth;
        record.progressToDepth = depth;
    }

    Vector3 RestoredStandingPosition(Transform player, View.State state, Vector3 current)
    {
        float footOffset = GetFootOffset(player);
        float footY = current.y + footOffset;
        float depth = AxisOf(current, state);
        TryGetRestoredDepth(state, current, footY, out depth);
        Vector3 standing = WithAxis(current, state, depth);
        if (TryGetRestoredSurfaceY(state, standing, footY, out float surfaceY))
        {
            standing.y = surfaceY - footOffset;
        }

        return standing;
    }

    void ApplySavedPlayerLanding(View.State state)
    {
        Record playerRecord = null;
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].kind == TargetKind.Player)
            {
                playerRecord = records[i];
                break;
            }
        }

        if (playerRecord == null || playerRecord.transform == null)
        {
            return;
        }

        Transform player = playerRecord.transform;
        Vector3 current = player.position;
        float footOffset = GetFootOffset(player);
        float footY = current.y + footOffset;
        Vector3 standing;
        if (TryFindBoxUnderFeet(state, current, footY, out Record standingBox, out float walkOffset))
        {
            standing = LandingOnSavedBox(standingBox, state, walkOffset, footOffset);
        }
        else
        {
            standing = RestoredStandingPosition(player, state, current);
        }
        Rigidbody body = playerRecord.rigidbody;
        if (body != null)
        {
            body.position = standing;
            Vector3 velocity = body.velocity;
            velocity.y = 0f;
            body.velocity = velocity;
        }

        player.position = standing;
        float depth = AxisOf(standing, state);
        playerRecord.progressFromDepth = depth;
        playerRecord.progressToDepth = depth;
        Physics.SyncTransforms();
    }

    bool TryGetRestoredSurfaceY(View.State state, Vector3 standing, float footY, out float surfaceY)
    {
        surfaceY = footY;
        bool found = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < restoredSurfaces.Count; i++)
        {
            RestoredSurface surface = restoredSurfaces[i];
            if (!MatchesExitView(surface, state) || !ContainsHorizontal(surface.bounds, standing))
            {
                continue;
            }

            ConsiderSurface(surface.walkTop, footY, ref surfaceY, ref found, ref bestDistance);
        }

        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.kind != TargetKind.Box || record.box == null || record.box.IsHeld)
            {
                continue;
            }

            Bounds bounds = record.supportBounds;
            if (!ContainsHorizontal(bounds, standing))
            {
                continue;
            }

            ConsiderSurface(bounds.max.y, footY, ref surfaceY, ref found, ref bestDistance);
        }

        return found;
    }

    static bool MatchesExitView(RestoredSurface surface, View.State state)
    {
        if (surface == null || !surface.shadow)
        {
            return surface != null;
        }

        return surface.platform != null && surface.platform.MatchesView(state);
    }

    static bool ContainsHorizontal(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x && point.x <= bounds.max.x &&
               point.z >= bounds.min.z && point.z <= bounds.max.z;
    }

    static void ConsiderSurface(
        float top,
        float footY,
        ref float surfaceY,
        ref bool found,
        ref float bestDistance)
    {
        // 高出脚底太多的是头顶的面，不能拿来当落点。
        if (top > footY + OverheadReject)
        {
            return;
        }

        float distance = Mathf.Abs(footY - top);
        if (found && distance >= bestDistance)
        {
            return;
        }

        found = true;
        bestDistance = distance;
        surfaceY = top;
    }

    static float SolidWalkTop(Transform target, Bounds fallback)
    {
        float top = float.NegativeInfinity;
        bool found = false;
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            top = Mathf.Max(top, collider.bounds.max.y);
            found = true;
        }

        return found ? top : fallback.max.y;
    }

    static float GetFootOffset(Transform player)
    {
        if (player == null)
        {
            return 0f;
        }

        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            return 0f;
        }

        Vector3 localBottom = capsule.center;
        float halfHeight = capsule.height * 0.5f;
        if (capsule.direction == 0)
        {
            localBottom.x -= halfHeight;
        }
        else if (capsule.direction == 2)
        {
            localBottom.z -= halfHeight;
        }
        else
        {
            localBottom.y -= halfHeight;
        }

        return player.TransformPoint(localBottom).y - player.position.y;
    }

    // 用脚底对正身下的地面。角色缩放是 (1.5, 2, 1.5)，脚底不能写死，要按胶囊体算。
    void StickPlayerToGround()
    {
        Transform player = view.transform;
        if (player == null)
        {
            return;
        }

        float footOffset = GetFootOffset(player);
        float footY = player.position.y + footOffset;
        Vector3 origin = new Vector3(player.position.x, footY + 3f, player.position.z);
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            12f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);

        bool found = false;
        float groundY = 0f;
        float bestGap = float.MaxValue;
        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;
            if (hitTransform == null || hitTransform == player || hitTransform.IsChildOf(player))
            {
                continue;
            }

            if (hits[i].normal.y < 0.45f)
            {
                continue;
            }

            if (hits[i].point.y > footY + 0.08f)
            {
                continue;
            }

            float gap = footY - hits[i].point.y;
            if (gap < bestGap)
            {
                bestGap = gap;
                groundY = hits[i].point.y;
                found = true;
            }
        }

        if (!found)
        {
            return;
        }

        Vector3 position = player.position;
        position.y = groundY - footOffset;
        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            Vector3 velocity = body.velocity;
            velocity.y = 0f;
            body.velocity = velocity;
        }

        player.position = position;
        Physics.SyncTransforms();
    }

    void CapturePlayerDepthScales(Record record, View.State state, Vector3 worldScale)
    {
        record.depthScaledChildren.Clear();
        record.depthScaledChildScales.Clear();
        record.childScaleFrom.Clear();
        record.childScaleTo.Clear();
        record.visualPoses.Clear();
        record.visualFlattenRoot = null;

        Transform player = record.transform;
        List<Transform> visuals = new List<Transform>();
        for (int i = 0; i < player.childCount; i++)
        {
            Transform child = player.GetChild(i);
            if (child.GetComponent<Rigidbody>() != null ||
                child.GetComponent<GroundChecker>() != null)
            {
                continue;
            }

            visuals.Add(child);
        }

        if (visuals.Count == 0 || record.scaleRoot == null)
        {
            return;
        }

        // 缩放放在没有转向的父节点上，再让模型保持自己的朝向。
        // 这样薄的那一侧始终是镜头深度：2DX 是世界 X，2DZ 是世界 Z。
        // 若直接缩模型的本地 Z，角色一面向 ±X，正视就会看到纸片的侧边。
        GameObject wrapperObject = new GameObject(player.name + "_FlattenAxis");
        Transform wrapper = wrapperObject.transform;
        wrapper.SetParent(record.scaleRoot, false);
        wrapper.localRotation = Quaternion.identity;
        wrapper.localScale = Vector3.one;
        wrapper.position = player.position;

        for (int i = 0; i < visuals.Count; i++)
        {
            Transform child = visuals[i];
            record.visualPoses.Add(new VisualPose
            {
                transform = child,
                siblingIndex = child.GetSiblingIndex(),
                localPosition = child.localPosition,
                localRotation = child.localRotation,
                localScale = child.localScale
            });
            child.SetParent(wrapper, true);
        }

        float depth = state == View.State.Version2DX ? worldScale.x : worldScale.z;
        Vector3 flatScale = Vector3.one;
        if (state == View.State.Version2DX)
        {
            flatScale.x = depth;
        }
        else
        {
            flatScale.z = depth;
        }

        record.visualFlattenRoot = wrapper;
        record.depthScaledChildren.Add(wrapper);
        record.depthScaledChildScales.Add(Vector3.one);
        record.childScaleFrom.Add(Vector3.one);
        record.childScaleTo.Add(flatScale);
    }

    void ReattachPlayerVisual(Record record)
    {
        Transform player = record.transform;
        for (int i = 0; i < record.visualPoses.Count; i++)
        {
            VisualPose pose = record.visualPoses[i];
            if (pose.transform == null || player == null)
            {
                continue;
            }

            pose.transform.SetParent(player, false);
            pose.transform.localPosition = pose.localPosition;
            pose.transform.localRotation = pose.localRotation;
            pose.transform.localScale = pose.localScale;
            int siblingIndex = Mathf.Clamp(
                pose.siblingIndex,
                0,
                Mathf.Max(0, player.childCount - 1));
            pose.transform.SetSiblingIndex(siblingIndex);
        }

        record.visualPoses.Clear();
        record.visualFlattenRoot = null;
    }

    static float AxisOf(Vector3 value, View.State state)
    {
        return state == View.State.Version2DX ? value.x : value.z;
    }

    static Vector3 WithAxis(Vector3 value, View.State state, float axis)
    {
        if (state == View.State.Version2DX)
        {
            value.x = axis;
        }
        else
        {
            value.z = axis;
        }

        return value;
    }

    static float WalkOf(Vector3 value, View.State state)
    {
        return state == View.State.Version2DX ? value.z : value.x;
    }

    static Vector3 WithWalk(Vector3 value, View.State state, float walk)
    {
        if (state == View.State.Version2DX)
        {
            value.z = walk;
        }
        else
        {
            value.x = walk;
        }

        return value;
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

    static List<Bounds> CaptureSolidColliderBounds(Transform target)
    {
        List<Bounds> bounds = new List<Bounds>();
        if (target == null)
        {
            return bounds;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            bounds.Add(collider.bounds);
        }

        return bounds;
    }

    // 每一级实心方块只留下没被更高一级盖住的踏面。盖住的后半截不再做碰撞。
    static List<Bounds> CaptureExposedStairTreads(Transform stair)
    {
        List<Bounds> solids = CaptureSolidColliderBounds(stair);
        List<Bounds> exposed = new List<Bounds>();
        for (int i = 0; i < solids.Count; i++)
        {
            if (TryExposeStairTread(solids, i, out Bounds tread))
            {
                exposed.Add(tread);
            }
        }

        return exposed;
    }

    static bool TryExposeStairTread(List<Bounds> solids, int index, out Bounds tread)
    {
        tread = solids[index];
        for (int i = 0; i < solids.Count; i++)
        {
            if (i == index || solids[i].max.y <= tread.max.y + 0.01f)
            {
                continue;
            }

            if (!TrimCoveredFootprint(ref tread, solids[i]))
            {
                return false;
            }
        }

        return tread.size.x > 0.05f && tread.size.y > 0.05f && tread.size.z > 0.05f;
    }

    // 更高一级若整段挡住左右或前后，就裁掉被挡住的那一侧，留下露出来的踏面。
    static bool TrimCoveredFootprint(ref Bounds tread, Bounds cover)
    {
        const float gap = 0.02f;
        float overlapMinX = Mathf.Max(tread.min.x, cover.min.x);
        float overlapMaxX = Mathf.Min(tread.max.x, cover.max.x);
        float overlapMinZ = Mathf.Max(tread.min.z, cover.min.z);
        float overlapMaxZ = Mathf.Min(tread.max.z, cover.max.z);
        if (overlapMaxX - overlapMinX <= gap || overlapMaxZ - overlapMinZ <= gap)
        {
            return true;
        }

        bool coversX = overlapMinX <= tread.min.x + gap && overlapMaxX >= tread.max.x - gap;
        bool coversZ = overlapMinZ <= tread.min.z + gap && overlapMaxZ >= tread.max.z - gap;
        if (coversX && coversZ)
        {
            return false;
        }

        if (coversX)
        {
            return KeepOpenSide(ref tread, axisZ: true, overlapMinZ, overlapMaxZ, gap);
        }

        if (coversZ)
        {
            return KeepOpenSide(ref tread, axisZ: false, overlapMinX, overlapMaxX, gap);
        }

        float takenX = (overlapMaxX - overlapMinX) / Mathf.Max(gap, tread.size.x);
        float takenZ = (overlapMaxZ - overlapMinZ) / Mathf.Max(gap, tread.size.z);
        if (takenZ >= takenX)
        {
            return KeepOpenSide(ref tread, axisZ: true, overlapMinZ, overlapMaxZ, gap);
        }

        return KeepOpenSide(ref tread, axisZ: false, overlapMinX, overlapMaxX, gap);
    }

    static bool KeepOpenSide(ref Bounds tread, bool axisZ, float cutMin, float cutMax, float gap)
    {
        Vector3 min = tread.min;
        Vector3 max = tread.max;
        int axis = axisZ ? 2 : 0;
        float lowSide = cutMin - min[axis];
        float highSide = max[axis] - cutMax;
        if (lowSide >= highSide && lowSide > gap)
        {
            max[axis] = cutMin - gap;
        }
        else if (highSide > gap)
        {
            min[axis] = cutMax + gap;
        }
        else
        {
            return false;
        }

        if (max[axis] - min[axis] <= gap)
        {
            return false;
        }

        tread.SetMinMax(min, max);
        return true;
    }

    // 踏面挂在不旋转的缩放节点上。深度轴跟着它变薄，高度和前后位置保持踏面原来的范围。
    bool AddStairStepColliders(Transform scaleRoot, List<Bounds> steps)
    {
        if (scaleRoot == null || steps == null || steps.Count == 0)
        {
            return false;
        }

        Vector3 rootScale = scaleRoot.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(rootScale.x));
        float sy = Mathf.Max(0.0001f, Mathf.Abs(rootScale.y));
        float sz = Mathf.Max(0.0001f, Mathf.Abs(rootScale.z));
        scaleRoot.gameObject.tag = "Ground";
        int added = 0;

        for (int i = 0; i < steps.Count; i++)
        {
            Bounds stepBounds = steps[i];
            if (stepBounds.size.sqrMagnitude < 0.000001f)
            {
                continue;
            }

            BoxCollider step = scaleRoot.gameObject.AddComponent<BoxCollider>();
            step.center = scaleRoot.InverseTransformPoint(stepBounds.center);
            step.size = new Vector3(
                stepBounds.size.x / sx,
                stepBounds.size.y / sy,
                stepBounds.size.z / sz);
            added++;
        }

        return added > 0;
    }

    bool AddStairRestoredSurfaces(Transform stair)
    {
        if (stair == null)
        {
            return false;
        }

        List<Bounds> treads = CaptureExposedStairTreads(stair);
        for (int i = 0; i < treads.Count; i++)
        {
            Bounds bounds = treads[i];
            restoredSurfaces.Add(new RestoredSurface
            {
                bounds = bounds,
                walkTop = bounds.max.y,
                shadow = false
            });
        }

        return treads.Count > 0;
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

    static bool TryGetSolidColliderHeight(Transform target, out float minY, out float maxY)
    {
        minY = float.PositiveInfinity;
        maxY = float.NegativeInfinity;
        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
            {
                continue;
            }

            minY = Mathf.Min(minY, collider.bounds.min.y);
            maxY = Mathf.Max(maxY, collider.bounds.max.y);
        }

        return maxY > minY;
    }

    static void FitColliderHeight(Transform scaleRoot, float minY, float maxY)
    {
        BoxCollider[] boxes = scaleRoot.GetComponents<BoxCollider>();
        if (boxes.Length == 0)
        {
            return;
        }

        BoxCollider wall = boxes[boxes.Length - 1];
        float sy = Mathf.Max(0.0001f, Mathf.Abs(scaleRoot.lossyScale.y));
        Vector3 size = wall.size;
        size.y = (maxY - minY) / sy;
        wall.size = size;

        Vector3 worldCenter = scaleRoot.position;
        worldCenter.y = (minY + maxY) * 0.5f;
        Vector3 center = wall.center;
        center.y = scaleRoot.InverseTransformPoint(worldCenter).y;
        wall.center = center;
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

    // 用进侧面时记下的深度判断谁更靠近镜头，用现在的投影判断箱子还盖不盖住物品。
    public bool IsItemBlockedByBox(Transform item)
    {
        if (!flattened || item == null)
        {
            return false;
        }

        if (animState != View.State.Version2DX && animState != View.State.Version2DZ)
        {
            return false;
        }

        Bounds itemBounds = ViewWorldBounds.Get(item);
        Rect itemRect = ViewWorldBounds.ProjectToView(itemBounds, animState);
        float itemDepth = DepthBeforeFlatten(item, itemBounds);

        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.kind != TargetKind.Box ||
                record.box == null ||
                record.box.IsHeld ||
                !record.box.gameObject.activeInHierarchy)
            {
                continue;
            }

            float boxDepth = ViewWorldBounds.CloserDepthScore(record.silhouetteBounds, animState);
            if (boxDepth <= itemDepth + DepthEpsilon)
            {
                continue;
            }

            Bounds boxBounds = ViewWorldBounds.GetSilhouette(record.box.transform);
            Rect boxRect = ViewWorldBounds.ProjectToView(boxBounds, animState);
            if (ViewWorldBounds.OverlapRatio(boxRect, itemRect) >= view.occlusionOverlapRatio)
            {
                return true;
            }
        }

        return false;
    }

    float DepthBeforeFlatten(Transform item, Bounds fallback)
    {
        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.transform == null)
            {
                continue;
            }

            if (record.transform == item || item.IsChildOf(record.transform))
            {
                return ViewWorldBounds.CloserDepthScore(record.silhouetteBounds, animState);
            }
        }

        return ViewWorldBounds.CloserDepthScore(fallback, animState);
    }

    void ApplyOcclusion(View.State state)
    {
        for (int i = 0; i < records.Count; i++)
        {
            Record target = records[i];
            if (target.transform == null ||
                target.kind == TargetKind.Player ||
                target.transform.GetComponent<ShadowPlatform>() != null ||
                target.transform.GetComponent<Key>() != null ||
                target.transform.GetComponent<PuzzleItem>() != null)
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

    // 放下被遮挡关掉的箱子。只重新打开这一只，不切视角。
    public void WakeDroppedBox(Box box)
    {
        if (box == null)
        {
            return;
        }

        for (int i = 0; i < records.Count; i++)
        {
            Record record = records[i];
            if (record.box != box)
            {
                continue;
            }

            record.deactivatedByOcclusion = false;
            record.wasHeld = false;
            record.rigidbodyKinematic = false;
            RestoreDisabledColliders(record);

            GameObject hideObject = record.scaleRoot != null
                ? record.scaleRoot.gameObject
                : box.gameObject;
            if (hideObject != null && !hideObject.activeSelf)
            {
                hideObject.SetActive(true);
            }

            if (!box.gameObject.activeSelf)
            {
                box.gameObject.SetActive(true);
            }

            return;
        }
    }

    // 压扁后的刚体缩放不均匀，薄薄的箱子对不齐同样被压薄的地面，松手就会穿下去。
    // 放下时另做一块均匀缩放的碰撞，深度和脚下的地面齐平，所以能落在地上。
    public bool TryDropFlattenedBox(
        Box box,
        Vector3 direction,
        float forwardForce,
        float upwardForce,
        Collider playerCollider,
        out Collider solidCollider)
    {
        solidCollider = null;
        if (!flattened || box == null)
        {
            return false;
        }

        if (animState != View.State.Version2DX && animState != View.State.Version2DZ)
        {
            return false;
        }

        Record record = FindBoxRecord(box);
        if (record == null || record.scaleRoot == null)
        {
            return false;
        }

        ReleaseDropBody(record, false);
        Bounds visualBounds = GetVisibleBounds(record.scaleRoot);
        if (visualBounds.size.sqrMagnitude < 0.0001f)
        {
            visualBounds = box.GetSolidBounds();
        }

        // 碰撞和模型用同一块范围。深度只把整块一起挪到地面上，模型和碰撞不会再分开。
        Vector3 center = visualBounds.center;
        if (TryFindDropSupport(visualBounds, record.scaleRoot, out Bounds support))
        {
            if (animState == View.State.Version2DX)
            {
                center.x = support.center.x;
            }
            else
            {
                center.z = support.center.z;
            }
        }

        Vector3 size = visualBounds.size;
        size.x = Mathf.Max(size.x, 0.05f);
        size.y = Mathf.Max(size.y, 0.05f);
        size.z = Mathf.Max(size.z, 0.05f);

        box.SuspendRigidbody();
        record.rigidbody = null;
        box.SetSolidTrigger(true);
        box.SetSolidColliderEnabled(true);

        GameObject bodyObject = new GameObject(box.name + "_DropBody");
        bodyObject.layer = box.gameObject.layer;
        bodyObject.tag = "Ground";
        bodyObject.transform.SetPositionAndRotation(visualBounds.center, Quaternion.identity);
        bodyObject.transform.localScale = Vector3.one;

        BoxCollider solid = bodyObject.AddComponent<BoxCollider>();
        solid.center = Vector3.zero;
        solid.size = size;
        solidCollider = solid;

        record.scaleRoot.SetParent(bodyObject.transform, true);
        bodyObject.transform.position = center;

        Rigidbody body = bodyObject.AddComponent<Rigidbody>();
        body.mass = Mathf.Max(0.01f, box.weight);
        body.drag = box.drag;
        body.angularDrag = box.angularDrag;
        body.useGravity = true;
        body.isKinematic = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = animState == View.State.Version2DX
            ? RigidbodyConstraints.FreezePositionX |
              RigidbodyConstraints.FreezeRotationX |
              RigidbodyConstraints.FreezeRotationY |
              RigidbodyConstraints.FreezeRotationZ
            : RigidbodyConstraints.FreezePositionZ |
              RigidbodyConstraints.FreezeRotationX |
              RigidbodyConstraints.FreezeRotationY |
              RigidbodyConstraints.FreezeRotationZ;
        body.velocity = direction * forwardForce + Vector3.up * upwardForce;
        body.angularVelocity = Vector3.zero;
        record.dropBodyRoot = bodyObject.transform;

        if (playerCollider != null)
        {
            Physics.IgnoreCollision(solid, playerCollider, true);
        }

        Physics.SyncTransforms();
        return true;
    }

    public void ReleaseFlattenedDropBody(Box box)
    {
        Record record = FindBoxRecord(box);
        if (record == null)
        {
            return;
        }

        ReleaseDropBody(record, false);
    }

    void ReleaseAllDropBodies()
    {
        for (int i = 0; i < records.Count; i++)
        {
            ReleaseDropBody(records[i], true);
        }
    }

    void ReleaseDropBody(Record record, bool enableSolid)
    {
        if (record.dropBodyRoot == null)
        {
            return;
        }

        Transform body = record.dropBodyRoot;
        if (record.scaleRoot != null && record.scaleRoot.parent == body)
        {
            record.scaleRoot.SetParent(null, true);
        }

        record.dropBodyRoot = null;
        UnityEngine.Object.Destroy(body.gameObject);

        if (record.box != null)
        {
            record.rigidbody = record.box.ResumeRigidbody();
            record.box.SetSolidTrigger(false);
            record.box.SetSolidColliderEnabled(enableSolid);
        }
    }

    static Bounds GetVisibleBounds(Transform root)
    {
        Bounds bounds = new Bounds();
        bool any = false;
        if (root == null)
        {
            return bounds;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            if (!any)
            {
                bounds = renderer.bounds;
                any = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    Record FindBoxRecord(Box box)
    {
        if (box == null)
        {
            return null;
        }

        for (int i = 0; i < records.Count; i++)
        {
            if (records[i].box == box)
            {
                return records[i];
            }
        }

        return null;
    }

    bool TryFindDropSupport(Bounds itemBounds, Transform ignoreRoot, out Bounds support)
    {
        support = default;
        if (view.transform == null)
        {
            return false;
        }

        float reach = DepthReach();
        float bestDistance = float.MaxValue;
        bool found = false;
        const int steps = 7;
        for (int step = 0; step < steps; step++)
        {
            float offset = Mathf.Lerp(-reach * 0.5f, reach * 0.5f, step / (float)(steps - 1));
            Vector3 origin = itemBounds.center;
            origin.y = itemBounds.min.y + 0.05f;
            if (animState == View.State.Version2DX)
            {
                origin.x = view.transform.position.x + offset;
            }
            else
            {
                origin.z = view.transform.position.z + offset;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                40f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null || hits[i].normal.y < 0.45f)
                {
                    continue;
                }

                Transform hitTransform = collider.transform;
                if (ignoreRoot != null &&
                    (hitTransform == ignoreRoot || hitTransform.IsChildOf(ignoreRoot)))
                {
                    continue;
                }

                if (hitTransform == view.transform || hitTransform.IsChildOf(view.transform))
                {
                    continue;
                }

                if (hits[i].distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hits[i].distance;
                support = collider.bounds;
                found = true;
            }
        }

        return found;
    }

    float DepthReach()
    {
        float reach = Mathf.Max(0.5f, view.flattenThickness);
        Collider body = view.GetComponent<Collider>();
        if (body == null)
        {
            return reach;
        }

        float axis = animState == View.State.Version2DX ? body.bounds.size.x : body.bounds.size.z;
        return Mathf.Max(reach, axis + view.flattenThickness);
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

    void RestorePlayer(Record record)
    {
        Vector3 current = record.transform.position;
        // 先把模型挂回角色，再拆缩放父节点，否则模型会跟着父节点一起被删掉。
        ReattachPlayerVisual(record);
        RestoreDepthScaledChildren(record);
        TearDownScaleRoot(record);

        record.transform.SetParent(record.originalParent, true);
        record.transform.localRotation = record.localRotation;
        record.transform.localScale = record.localScale;

        Vector3 restored = current;

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
