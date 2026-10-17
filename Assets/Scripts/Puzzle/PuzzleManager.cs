using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================================
// PuzzleManager —— 每个关卡场景放一个（挂在一个空物体上即可）
// ----------------------------------------------------------------------------
// 负责：
//   1. 记录本关已经收集到了哪些碎片（PuzzleItem 碰到玩家时调用 CollectPuzzle）
//   2. 按 F 键打开 / 关闭拼图 UI 面板（任何地方都能按）
//   3. 面板打开时暂停游戏、显示鼠标、禁用玩家操作脚本
//   4. 拼图完成后（由 PuzzleBoard 调用 OnPuzzleSolved）进入下一关
// ============================================================================
public class PuzzleManager : MonoBehaviour
{
    // 单例：方便 PuzzleItem / PuzzleBoard 通过 PuzzleManager.Instance 找到它
    public static PuzzleManager Instance;

    [Header("碎片数量")]
    [Tooltip("本关拼图一共有多少块碎片")]
    public int totalPuzzles = 12;

    [Header("拼图面板")]
    [Tooltip("拼图 UI 面板（Canvas 下的一个 Panel，上面挂 PuzzleBoard）。场景开始时请让它处于隐藏状态")]
    public GameObject puzzlePanel;

    [Tooltip("打开 / 关闭拼图面板的按键")]
    public KeyCode toggleKey = KeyCode.F;

    [Tooltip("面板打开期间要禁用的脚本，例如玩家身上的 PlayerController、View、InteractableChecker，" +
             "防止拼图时按 Q/E/R 等键还会影响游戏")]
    public Behaviour[] disableWhilePuzzleOpen;

    [Header("下一关")]
    [Tooltip("拼完后要加载的场景名。留空 = 自动加载 Build Settings 里的下一个场景（当前序号 + 1）")]
    public string nextSceneName = "";

    [Tooltip("拼完后等待几秒再切换场景（让玩家看一眼拼好的图）")]
    public float loadDelay = 1.5f;

    // 已收集的碎片 ID 列表
    private List<int> collectPuzzles = new List<int>();

    // 拼图是否已经拼好（拼好后不能再按 F 关闭/打开面板）
    public bool isPuzzleCompleted = false;

    // 面板当前是否打开
    public bool IsPanelOpen { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // 切换场景时本物体会被销毁，必须清空静态引用，
        // 否则下一关的 PuzzleManager 会因为 Instance 不为空而把自己销毁
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        // 保证一开始面板是关着的
        if (puzzlePanel != null)
        {
            puzzlePanel.SetActive(false);
        }
    }

    private void Update()
    {
        // 拼好之后正在等待切关，不再响应 F
        if (isPuzzleCompleted)
        {
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            if (IsPanelOpen)
            {
                ClosePanel();
            }
            else
            {
                OpenPanel();
            }
        }
    }

    // ---------------------------------------------------------------- 面板开关

    public void OpenPanel()
    {
        if (puzzlePanel == null)
        {
            Debug.LogWarning("PuzzleManager：没有设置 puzzlePanel");
            return;
        }

        IsPanelOpen = true;
        // 面板被激活时，PuzzleBoard.OnEnable 会根据已收集的碎片刷新显示
        puzzlePanel.SetActive(true);

        // 暂停游戏世界（物理、动画等停止）。UI 的拖拽不受 timeScale 影响，仍然可以操作
        Time.timeScale = 0f;

        // 显示并解锁鼠标，方便拖动碎片
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SetPlayerScriptsEnabled(false);
    }

    public void ClosePanel()
    {
        IsPanelOpen = false;
        if (puzzlePanel != null)
        {
            puzzlePanel.SetActive(false);
        }

        // 恢复游戏
        Time.timeScale = 1f;
        SetPlayerScriptsEnabled(true);
    }

    // 批量启用 / 禁用 Inspector 里指定的脚本
    private void SetPlayerScriptsEnabled(bool enabled)
    {
        if (disableWhilePuzzleOpen == null)
        {
            return;
        }

        foreach (Behaviour behaviour in disableWhilePuzzleOpen)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }
    }

    // ---------------------------------------------------------------- 碎片收集

    // PuzzleItem 被玩家碰到时调用
    public void CollectPuzzle(int puzzleID)
    {
        if (!collectPuzzles.Contains(puzzleID))
        {
            collectPuzzles.Add(puzzleID);
            Debug.Log("收集到碎片 " + puzzleID + "，当前共 " + collectPuzzles.Count + " / " + totalPuzzles);
            CheckPuzzleCount();
        }
    }

    // 只是提示"碎片已经全部收集"，真正的"完成"要等玩家在面板里拼好
    public void CheckPuzzleCount()
    {
        if (collectPuzzles.Count >= totalPuzzles)
        {
            Debug.Log("碎片已全部收集！按 " + toggleKey + " 打开拼图");
        }
    }

    // 某个碎片是否已经收集（PuzzleBoard 用它决定碎片是否显示）
    public bool HasPuzzle(int puzzleID)
    {
        return collectPuzzles.Contains(puzzleID);
    }

    // ---------------------------------------------------------------- 拼图完成

    // 由 PuzzleBoard 在所有碎片连成一整块时调用
    public void OnPuzzleSolved()
    {
        if (isPuzzleCompleted)
        {
            return;
        }

        isPuzzleCompleted = true;
        Debug.Log("拼图完成！即将进入下一关");
        StartCoroutine(LoadNextLevel());
    }

    private IEnumerator LoadNextLevel()
    {
        // 游戏暂停时 timeScale = 0，普通的 WaitForSeconds 会永远等不完，
        // 所以这里要用不受 timeScale 影响的 WaitForSecondsRealtime
        yield return new WaitForSecondsRealtime(loadDelay);

        // 切场景前恢复时间流速，否则下一关一开始就是暂停的
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            yield break;
        }

        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            Debug.LogWarning("PuzzleManager：已经是 Build Settings 中的最后一个场景，没有下一关可加载");
        }
    }
}
