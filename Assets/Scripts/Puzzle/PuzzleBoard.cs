using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================================
// PuzzleBoard —— 挂在拼图面板上，管理所有 PuzzlePiece
// ----------------------------------------------------------------------------
// 操作：
//   左键点在碎片上 —— 抓起它所在的整组，碎片跟着鼠标
//   再点一次左键 —— 放下。靠得够近、方向相同、原图里相邻的边会吸在一起
//   右键 —— 把鼠标下的那一组逆时针转 90°。没点到碎片时，转正在抓着的那组
//
// 全部碎片连成一组后通知 PuzzleManager。
// ============================================================================
public class PuzzleBoard : MonoBehaviour
{
    [Tooltip("所有碎片的父物体。碎片必须是它的直接子物体")]
    public RectTransform pieceRoot;

    [Tooltip("相邻两块中心点的距离。和格子大小一致，不含碎片之间故意留的缝")]
    public Vector2 pieceSize = new Vector2(120f, 120f);

    [Tooltip("边距离正确位置多近时吸附")]
    public float snapDistance = 48f;

    [Tooltip("第一次打开时，每块随机转 0°、90°、180° 或 270°")]
    public bool randomRotationOnStart = true;

    const float HeldScale = 1.06f;

    readonly List<PuzzlePiece> pieces = new List<PuzzlePiece>();
    readonly List<RaycastResult> raycasts = new List<RaycastResult>();

    PointerEventData pointer;
    Canvas canvas;
    PuzzlePiece held;
    Vector2 grabOffset;
    bool initialized;
    bool locked;

    void Awake()
    {
        Initialize();
    }

    void OnEnable()
    {
        RefreshVisibility();
    }

    void OnDisable()
    {
        // 面板被直接关掉时，不要在销毁过程里再吸附
        if (held != null)
        {
            SetScale(held, 1f);
            held = null;
        }
    }

    void Update()
    {
        if (locked)
        {
            return;
        }

        FollowHeld();

        if (Input.GetMouseButtonDown(1))
        {
            HandleRightClick();
        }

        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClick();
        }
    }

    void Initialize()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        if (pieceRoot == null)
        {
            pieceRoot = (RectTransform)transform;
        }

        canvas = GetComponentInParent<Canvas>();
        pieceRoot.GetComponentsInChildren(true, pieces);

        foreach (PuzzlePiece piece in pieces)
        {
            if (randomRotationOnStart)
            {
                piece.rotationStep = Random.Range(0, 4);
            }

            piece.Init();
        }
    }

    // 已收集的显示，没收集的隐藏
    void RefreshVisibility()
    {
        Initialize();

        foreach (PuzzlePiece piece in pieces)
        {
            bool collected = PuzzleProgress.Contains(piece.puzzleID);
            piece.gameObject.SetActive(collected);
        }
    }

    // ---------------------------------------------------------------- 抓取和放下

    void HandleLeftClick()
    {
        if (held != null)
        {
            ReleaseHeld(true);
            return;
        }

        PuzzlePiece under = PieceUnderMouse();
        if (under != null)
        {
            Grab(under);
        }
    }

    void HandleRightClick()
    {
        PuzzlePiece under = PieceUnderMouse();
        PuzzlePiece pivot = under != null ? under : held;
        if (pivot == null)
        {
            return;
        }

        RotateGroup(pivot);

        if (held != null && held.group != null && held.group.Contains(pivot))
        {
            grabOffset = held.Rect.anchoredPosition - MouseInRoot();
            SetScale(held, HeldScale);
        }
    }

    void Grab(PuzzlePiece piece)
    {
        held = piece;
        grabOffset = piece.Rect.anchoredPosition - MouseInRoot();

        foreach (PuzzlePiece part in piece.group)
        {
            part.Rect.SetAsLastSibling();
        }

        SetScale(piece, HeldScale);
    }

    // 松手。snap 为 true 时检查能不能和旁边的碎片吸上
    public void ReleaseHeld(bool snap)
    {
        if (held == null)
        {
            return;
        }

        PuzzlePiece piece = held;
        SetScale(piece, 1f);
        held = null;

        if (snap)
        {
            TrySnap(piece);
        }
    }

    public void Lock()
    {
        locked = true;
        ReleaseHeld(false);
    }

    void FollowHeld()
    {
        if (held == null)
        {
            return;
        }

        Vector2 target = MouseInRoot() + grabOffset;
        Rect rect = pieceRoot.rect;
        target.x = Mathf.Clamp(target.x, rect.xMin + 56f, rect.xMax - 56f);
        target.y = Mathf.Clamp(target.y, rect.yMin + 56f, rect.yMax - 56f);

        Vector2 delta = target - held.Rect.anchoredPosition;
        foreach (PuzzlePiece part in held.group)
        {
            part.Rect.anchoredPosition += delta;
        }
    }

    PuzzlePiece PieceUnderMouse()
    {
        if (EventSystem.current == null)
        {
            return null;
        }

        if (pointer == null)
        {
            pointer = new PointerEventData(EventSystem.current);
        }

        pointer.position = Input.mousePosition;
        raycasts.Clear();
        EventSystem.current.RaycastAll(pointer, raycasts);

        for (int i = 0; i < raycasts.Count; i++)
        {
            PuzzlePiece piece = raycasts[i].gameObject.GetComponentInParent<PuzzlePiece>();
            if (piece != null && piece.gameObject.activeInHierarchy)
            {
                return piece;
            }
        }

        return null;
    }

    Vector2 MouseInRoot()
    {
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            pieceRoot,
            Input.mousePosition,
            cam,
            out Vector2 local);
        return local;
    }

    static void SetScale(PuzzlePiece piece, float scale)
    {
        if (piece == null || piece.group == null)
        {
            return;
        }

        foreach (PuzzlePiece part in piece.group)
        {
            part.Rect.localScale = Vector3.one * scale;
        }
    }

    // ---------------------------------------------------------------- 旋转

    // 以 pivot 为中心，把整组逆时针转 90°
    public void RotateGroup(PuzzlePiece pivot)
    {
        Vector2 center = pivot.Rect.anchoredPosition;

        foreach (PuzzlePiece piece in pivot.group)
        {
            // (x, y) 逆时针 90° 是 (-y, x)
            Vector2 offset = piece.Rect.anchoredPosition - center;
            piece.Rect.anchoredPosition = center + new Vector2(-offset.y, offset.x);
            piece.rotationStep = (piece.rotationStep + 1) % 4;
            piece.ApplyRotation();
        }

        TrySnap(pivot);
    }

    // ---------------------------------------------------------------- 吸附

    // from 在当前角度下，to 的中心应该在哪
    Vector2 ExpectedPosition(PuzzlePiece from, PuzzlePiece to)
    {
        // 没旋转时：列大的在右边，行大的在下边（UI 的 y 朝上，所以行差要取负）
        Vector2 offset = new Vector2(
            (to.col - from.col) * pieceSize.x,
            -(to.row - from.row) * pieceSize.y);

        offset = Quaternion.Euler(0f, 0f, from.rotationStep * 90f) * offset;
        return from.Rect.anchoredPosition + offset;
    }

    static bool AreNeighbors(PuzzlePiece a, PuzzlePiece b)
    {
        return Mathf.Abs(a.row - b.row) + Mathf.Abs(a.col - b.col) == 1;
    }

    public void TrySnap(PuzzlePiece moved)
    {
        if (moved == null || moved.group == null)
        {
            return;
        }

        bool snapped = true;

        // 吸上一块以后整组位置变了，可能还能再吸上下一块，所以要循环
        while (snapped)
        {
            snapped = false;

            foreach (PuzzlePiece mine in moved.group)
            {
                foreach (PuzzlePiece other in pieces)
                {
                    if (!other.gameObject.activeSelf) continue;
                    if (moved.group.Contains(other)) continue;
                    if (!AreNeighbors(mine, other)) continue;
                    if (mine.rotationStep != other.rotationStep) continue;

                    Vector2 target = ExpectedPosition(other, mine);
                    Vector2 error = target - mine.Rect.anchoredPosition;

                    if (error.magnitude <= snapDistance)
                    {
                        foreach (PuzzlePiece part in moved.group)
                        {
                            part.Rect.anchoredPosition += error;
                        }

                        MergeGroups(moved.group, other.group);
                        snapped = true;
                        break;
                    }
                }

                if (snapped)
                {
                    break;
                }
            }
        }

        CheckComplete(moved);
    }

    static void MergeGroups(List<PuzzlePiece> a, List<PuzzlePiece> b)
    {
        foreach (PuzzlePiece piece in b)
        {
            a.Add(piece);
            piece.group = a;
        }
    }

    void CheckComplete(PuzzlePiece anyPiece)
    {
        PuzzleManager manager = PuzzleManager.Instance;
        if (manager == null || anyPiece == null || anyPiece.group == null)
        {
            return;
        }

        if (anyPiece.group.Count >= manager.totalPuzzles)
        {
            manager.OnPuzzleSolved();
        }
    }
}
