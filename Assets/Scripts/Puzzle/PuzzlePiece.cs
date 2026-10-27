using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// PuzzlePiece —— 拼图面板里的一块 UI 碎片
// ----------------------------------------------------------------------------
// 挂在一块 UI Image 上，填 puzzleID、row、col。
// 同一块的世界收集物 PuzzleItem.puzzleID 必须和这里一样。
//
// row / col 是它在拼好的图里的位置：
//   12 块、3 行 4 列时，左上角是 row=0 col=0，右下角是 row=2 col=3。
// 抓取、放下、旋转由 PuzzleBoard 统一处理，这块脚本只记数据。
// ============================================================================
[RequireComponent(typeof(RectTransform))]
public class PuzzlePiece : MonoBehaviour
{
    [Tooltip("碎片编号，必须和关卡里对应 PuzzleItem 的 puzzleID 相同")]
    public int puzzleID;

    [Tooltip("在完整图中的行号（从 0 开始，最上面一行是 0）")]
    public int row;

    [Tooltip("在完整图中的列号（从 0 开始，最左边一列是 0）")]
    public int col;

    // 当前转了几个 90°。0 是正方向（数字朝上）
    [HideInInspector] public int rotationStep;

    // 和它对上边的碎片共用这一个列表，之后一起移动、一起转
    [HideInInspector] public List<PuzzlePiece> group;

    public RectTransform Rect { get; private set; }

    public void Init()
    {
        Rect = GetComponent<RectTransform>();
        group = new List<PuzzlePiece> { this };
        ApplyRotation();
    }

    public void ApplyRotation()
    {
        Rect.localEulerAngles = new Vector3(0f, 0f, rotationStep * 90f);
    }
}
