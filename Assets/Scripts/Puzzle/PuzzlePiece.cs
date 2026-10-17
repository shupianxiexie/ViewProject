using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// ============================================================================
// PuzzlePiece —— 拼图面板里的一块 UI 碎片
// ----------------------------------------------------------------------------
// 用法：
//   在拼图面板下自己创建 UI Image（GameObject > UI > Image），放上碎片图片，
//   挂上本脚本，填好 puzzleID、row、col。所有碎片必须是同一个父物体的直接子物体。
//
// 操作：
//   鼠标左键拖动 —— 移动这块碎片（如果它已经和别的碎片连在一起，整组一起移动）
//   鼠标右键单击 —— 整组逆时针旋转 90°
//
// row / col 是这块碎片在"拼好的完整图"中的位置：
//   比如 12 块拼成 3 行 4 列，左上角是 row=0 col=0，右下角是 row=2 col=3。
//   程序根据它判断哪两块是相邻的、相邻的边应该怎么对齐。
// ============================================================================
[RequireComponent(typeof(RectTransform))]
public class PuzzlePiece : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Tooltip("碎片编号，必须和关卡里对应 PuzzleItem 的 puzzleID 相同")]
    public int puzzleID;

    [Tooltip("在完整图中的行号（从 0 开始，最上面一行是 0）")]
    public int row;

    [Tooltip("在完整图中的列号（从 0 开始，最左边一列是 0）")]
    public int col;

    // 当前旋转了几个 90°（0~3）。0 = 正确朝向
    [HideInInspector] public int rotationStep;

    // 这块碎片所在的"组"。一开始每块自己一组；
    // 两块拼上之后，它们共用同一个 List，之后一起移动和旋转
    [HideInInspector] public List<PuzzlePiece> group;

    public RectTransform Rect { get; private set; }

    private PuzzleBoard board;
    private Canvas canvas;
    private bool dragging;

    // 由 PuzzleBoard 在初始化时调用
    public void Init(PuzzleBoard owner)
    {
        board = owner;
        Rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        group = new List<PuzzlePiece> { this };
        ApplyRotation();
    }

    // 把 rotationStep 应用到物体的 Z 轴旋转上
    public void ApplyRotation()
    {
        Rect.localEulerAngles = new Vector3(0f, 0f, rotationStep * 90f);
    }

    // ---------------------------------------------------------------- 拖动

    public void OnBeginDrag(PointerEventData eventData)
    {
        // 只允许左键拖动
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        dragging = true;

        // 把整组移到最上层显示，防止被其他碎片挡住
        foreach (PuzzlePiece piece in group)
        {
            piece.Rect.SetAsLastSibling();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        // eventData.delta 是鼠标移动的屏幕像素，
        // 除以 Canvas 的缩放系数才是 UI 坐标里的移动量
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 delta = eventData.delta / scale;

        // 整组一起移动
        foreach (PuzzlePiece piece in group)
        {
            piece.Rect.anchoredPosition += delta;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging)
        {
            return;
        }

        dragging = false;

        // 松手时检查有没有相邻碎片的边靠得足够近，能拼上就吸附
        board.TrySnap(this);
    }

    // ---------------------------------------------------------------- 旋转

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            board.RotateGroup(this);
        }
    }
}
