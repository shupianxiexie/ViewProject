using System.Collections.Generic;

// ============================================================================
// PuzzleProgress —— 本次播放期间的拼图收集进度
// ----------------------------------------------------------------------------
// 不挂在任何物体上。收集到的碎片编号记在静态集合里，
// 所以从一个场景切到另一个场景后，已经收集的碎片还在。
// 停止播放（退出 Play）后会被清空，不会写到硬盘。
// ============================================================================
public static class PuzzleProgress
{
    static readonly HashSet<int> collected = new HashSet<int>();

    public static int Count => collected.Count;

    public static bool Contains(int puzzleID)
    {
        return collected.Contains(puzzleID);
    }

    // 返回 true 表示这是新收集到的一块
    public static bool Collect(int puzzleID)
    {
        return collected.Add(puzzleID);
    }
}
