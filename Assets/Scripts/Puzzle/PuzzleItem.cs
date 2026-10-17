using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ============================================================================
// PuzzleItem —— 关卡里可以被玩家捡起的拼图碎片（3D 物体）
// ----------------------------------------------------------------------------
// 用法：放在场景里，需要一个勾选了 Is Trigger 的 Collider。
// puzzleID 要和拼图面板里对应 PuzzlePiece 的 puzzleID 一致，
// 这样捡到它之后，面板里对应的那块碎片才会显示出来。
// ============================================================================
public class PuzzleItem : InteractableItem
{
    [Tooltip("碎片编号，必须和拼图面板中对应 PuzzlePiece 的 puzzleID 相同")]
    public int puzzleID;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (PuzzleManager.Instance == null)
        {
            Debug.LogWarning("PuzzleItem：场景中没有 PuzzleManager");
            return;
        }

        // 原来调用的是 CompletePuzzle，会导致捡到第一块就算完成；现在改为"收集"
        PuzzleManager.Instance.CollectPuzzle(puzzleID);
        Destroy(gameObject);
    }
}
