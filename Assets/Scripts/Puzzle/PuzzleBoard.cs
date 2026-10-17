using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// PuzzleBoard —— 挂在拼图 UI 面板上，管理所有 PuzzlePiece
// ----------------------------------------------------------------------------
// 负责：
//   1. 面板打开时，只显示已经收集到的碎片
//   2. 处理碎片之间的"按边吸附"：
//        两块碎片在完整图中相邻（行或列差 1）、
//        旋转角度相同、
//        并且它们对应的边靠得足够近 → 自动对齐并合并成一组
//   3. 所有碎片合并成一组时 → 通知 PuzzleManager 拼图完成
//
// 层级结构示例：
//   Canvas
//     └─ PuzzlePanel        (挂 PuzzleBoard，拖给 PuzzleManager.puzzlePanel)
//          ├─ Background    (可选，半透明背景)
//          └─ PieceArea     (空的 RectTransform，拖给本脚本的 pieceRoot)
//               ├─ Piece_0  (UI Image + PuzzlePiece)
//               ├─ Piece_1
//               └─ ...共 12 块
// ============================================================================
public class PuzzleBoard : MonoBehaviour
{
    [Tooltip("所有碎片的父物体。碎片必须是它的直接子物体，这样它们的坐标才在同一个坐标系里")]
    public RectTransform pieceRoot;

    [Tooltip("单块碎片的宽和高（UI 单位）。相邻两块中心点之间的距离就是这个值。" +
             "如果你的碎片图片带凸起，这里填不含凸起的格子大小")]
    public Vector2 pieceSize = new Vector2(100f, 100f);

    [Tooltip("两块碎片的边距离正确位置多近时自动吸附（UI 单位）")]
    public float snapDistance = 25f;

    [Tooltip("开始时是否给每块碎片随机一个旋转角度，增加难度")]
    public bool randomRotationOnStart = true;

    private readonly List<PuzzlePiece> pieces = new List<PuzzlePiece>();
    private bool initialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        // 每次按 F 打开面板时，刷新一次哪些碎片可见
        RefreshVisibility();
    }

    private void Initialize()
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

        // 找到 pieceRoot 下所有碎片（包括被隐藏的）
        pieceRoot.GetComponentsInChildren(true, pieces);

        foreach (PuzzlePiece piece in pieces)
        {
            if (randomRotationOnStart)
            {
                piece.rotationStep = Random.Range(0, 4);
            }
            piece.Init(this);
        }
    }

    // 已收集的碎片显示，没收集的隐藏
    private void RefreshVisibility()
    {
        Initialize();

        PuzzleManager manager = PuzzleManager.Instance;
        foreach (PuzzlePiece piece in pieces)
        {
            bool collected = manager != null && manager.HasPuzzle(piece.puzzleID);
            piece.gameObject.SetActive(collected);
        }
    }

    // ---------------------------------------------------------------- 旋转

    // 以被点击的那块碎片为中心，把它所在的整组逆时针旋转 90°
    public void RotateGroup(PuzzlePiece pivot)
    {
        Vector2 center = pivot.Rect.anchoredPosition;

        foreach (PuzzlePiece piece in pivot.group)
        {
            // 组内其他碎片绕中心点转 90°：向量 (x, y) 逆时针转 90° 后是 (-y, x)
            Vector2 offset = piece.Rect.anchoredPosition - center;
            piece.Rect.anchoredPosition = center + new Vector2(-offset.y, offset.x);

            // 每块碎片自身的朝向也加 90°
            piece.rotationStep = (piece.rotationStep + 1) % 4;
            piece.ApplyRotation();
        }

        // 旋转后可能刚好和别的碎片对上
        TrySnap(pivot);
    }

    // ---------------------------------------------------------------- 吸附

    // 计算：当 from 处于当前位置和角度时，to 应该在的正确位置
    private Vector2 ExpectedPosition(PuzzlePiece from, PuzzlePiece to)
    {
        // 未旋转时 to 相对 from 的偏移：
        //   列多 1 → 往右一个宽度；行多 1 → 往下一个高度（UI 的 y 轴向上，所以是负号）
        Vector2 offset = new Vector2(
            (to.col - from.col) * pieceSize.x,
            -(to.row - from.row) * pieceSize.y);

        // 再按 from 的旋转角度把偏移转过去
        offset = Quaternion.Euler(0f, 0f, from.rotationStep * 90f) * offset;

        return from.Rect.anchoredPosition + offset;
    }

    // 两块在完整图中是否上下或左右相邻（斜对角不算）
    private static bool AreNeighbors(PuzzlePiece a, PuzzlePiece b)
    {
        return Mathf.Abs(a.row - b.row) + Mathf.Abs(a.col - b.col) == 1;
    }

    // 检查 moved 所在的组能不能和别的碎片拼上，能就吸附并合并
    public void TrySnap(PuzzlePiece moved)
    {
        bool snapped = true;

        // 用循环是因为：拼上一块后，新组的位置变了，可能又能和别的碎片拼上
        while (snapped)
        {
            snapped = false;

            foreach (PuzzlePiece mine in moved.group)
            {
                foreach (PuzzlePiece other in pieces)
                {
                    // 跳过：未收集（隐藏）的、已经在同一组的、不相邻的、角度不同的
                    if (!other.gameObject.activeSelf) continue;
                    if (moved.group.Contains(other)) continue;
                    if (!AreNeighbors(mine, other)) continue;
                    if (mine.rotationStep != other.rotationStep) continue;

                    // mine 按 other 的位置来算，"应该"在哪里
                    Vector2 target = ExpectedPosition(other, mine);
                    Vector2 error = target - mine.Rect.anchoredPosition;

                    if (error.magnitude <= snapDistance)
                    {
                        // 把自己整组挪过去，让边完全对齐
                        foreach (PuzzlePiece p in moved.group)
                        {
                            p.Rect.anchoredPosition += error;
                        }

                        MergeGroups(moved.group, other.group);
                        snapped = true;
                        break;
                    }
                }

                // 组已经变了，foreach 不能继续用旧列表，跳出重新检查
                if (snapped) break;
            }
        }

        CheckComplete(moved);
    }

    // 把组 b 并入组 a，并让所有成员指向同一个列表
    private static void MergeGroups(List<PuzzlePiece> a, List<PuzzlePiece> b)
    {
        foreach (PuzzlePiece p in b)
        {
            a.Add(p);
            p.group = a;
        }
    }

    // ---------------------------------------------------------------- 完成判断

    private void CheckComplete(PuzzlePiece anyPiece)
    {
        PuzzleManager manager = PuzzleManager.Instance;
        if (manager == null)
        {
            return;
        }

        // 条件：一组里的碎片数量达到总数（意味着全部收集并且全部连在一起）
        if (anyPiece.group.Count >= manager.totalPuzzles)
        {
            manager.OnPuzzleSolved();
        }
    }
}
