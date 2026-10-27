using UnityEngine;

// ============================================================================
// PuzzleItem —— 关卡里可以捡起的拼图碎片（3D 物体）
// ----------------------------------------------------------------------------
// 需要一个勾选了 Is Trigger 的 Collider，并且玩家物体的 Tag 是 Player。
// puzzleID 要和拼图面板里对应 PuzzlePiece 的 puzzleID 一致。
// ============================================================================
public class PuzzleItem : InteractableItem
{
    [Tooltip("碎片编号，必须和拼图面板中对应 PuzzlePiece 的 puzzleID 相同")]
    public int puzzleID;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.CollectPuzzle(puzzleID);
        }
        else if (PuzzleProgress.Collect(puzzleID))
        {
            // 这个场景没放面板也可以先记下，之后打开有面板的场景还能看到
            Debug.Log("Collected piece " + puzzleID + ". This scene has no PuzzleManager, but the piece was saved");
        }

        Destroy(gameObject);
    }
}
