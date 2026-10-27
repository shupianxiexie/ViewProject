using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// ============================================================================
// PuzzleManager —— 每个需要打开拼图的场景放一个（挂在空物体上）
// ----------------------------------------------------------------------------
// 负责：
//   1. 按 P 打开 / 关闭拼图界面。没收集也能打开，只是里面没有碎片
//   2. 打开时暂停游戏、显示鼠标，并禁用指定的玩家脚本
//   3. 拼图完成后进入 nextSceneName（测试场景里是 FinalLevel）
// 收集记录本身在 PuzzleProgress 里，切场景不会丢。
// ============================================================================
public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance;

    [Header("碎片数量")]
    [Tooltip("拼成一整块需要多少块。测试场景是 3 行 4 列，共 12 块")]
    public int totalPuzzles = 12;

    [Header("拼图面板")]
    [Tooltip("拼图 UI 面板。场景开始时保持隐藏")]
    public GameObject puzzlePanel;

    [Tooltip("打开 / 关闭拼图面板的按键")]
    public KeyCode toggleKey = KeyCode.P;

    [Tooltip("面板打开期间要禁用的脚本，避免走动和拼图抢输入")]
    public Behaviour[] disableWhilePuzzleOpen;

    [Tooltip("屏幕上方的收集进度文字，可以留空")]
    public UnityEngine.UI.Text collectedLabel;

    [Tooltip("拼好后才显示的提示，可以留空")]
    public GameObject solvedMessage;

    [Header("完成后")]
    [Tooltip("拼完后要加载的场景名。测试场景填 FinalLevel")]
    public string nextSceneName = "";

    [Tooltip("拼完后停留几秒，让玩家看一眼拼好的图")]
    public float loadDelay = 1.5f;

    public bool isPuzzleCompleted = false;

    public bool IsPanelOpen { get; private set; }

    CursorLockMode cursorLockBefore;
    bool cursorVisibleBefore;

    void Awake()
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

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        if (puzzlePanel != null)
        {
            puzzlePanel.SetActive(false);
        }

        if (solvedMessage != null)
        {
            solvedMessage.SetActive(false);
        }

        RefreshLabel();
    }

    void Update()
    {
        if (isPuzzleCompleted)
        {
            return;
        }

        if (!Input.GetKeyDown(toggleKey))
        {
            return;
        }

        if (IsPanelOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    // ---------------------------------------------------------------- 面板开关

    public void OpenPanel()
    {
        if (IsPanelOpen)
        {
            return;
        }

        if (puzzlePanel == null)
        {
            Debug.LogWarning("PuzzleManager: puzzlePanel is not assigned");
            return;
        }

        cursorLockBefore = Cursor.lockState;
        cursorVisibleBefore = Cursor.visible;

        IsPanelOpen = true;
        puzzlePanel.SetActive(true);

        // 暂停世界。拼图脚本的 Update 不受 timeScale 影响，仍然能点选
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        SetPlayerScriptsEnabled(false);
    }

    public void ClosePanel()
    {
        if (!IsPanelOpen)
        {
            return;
        }

        // 先松手并吸附。如果这一下刚好拼完，面板留着给玩家看
        if (puzzlePanel != null)
        {
            PuzzleBoard board = puzzlePanel.GetComponent<PuzzleBoard>();
            if (board != null)
            {
                board.ReleaseHeld(true);
            }
        }

        if (isPuzzleCompleted)
        {
            return;
        }

        if (puzzlePanel != null)
        {
            puzzlePanel.SetActive(false);
        }

        IsPanelOpen = false;
        Time.timeScale = 1f;
        Cursor.lockState = cursorLockBefore;
        Cursor.visible = cursorVisibleBefore;
        SetPlayerScriptsEnabled(true);
    }

    void SetPlayerScriptsEnabled(bool enabled)
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

    // PuzzleItem 碰到玩家时调用
    public void CollectPuzzle(int puzzleID)
    {
        bool added = PuzzleProgress.Collect(puzzleID);
        OnPuzzleCollected(puzzleID, added);
    }

    public void OnPuzzleCollected(int puzzleID, bool added)
    {
        if (added)
        {
            Debug.Log("Collected piece " + puzzleID + ". " + PuzzleProgress.Count + " / " + totalPuzzles);
        }

        RefreshLabel();

        if (PuzzleProgress.Count >= totalPuzzles)
        {
            Debug.Log("All pieces collected. Press " + toggleKey + " to open the puzzle");
        }
    }

    public bool HasPuzzle(int puzzleID)
    {
        return PuzzleProgress.Contains(puzzleID);
    }

    void RefreshLabel()
    {
        if (collectedLabel == null || isPuzzleCompleted)
        {
            return;
        }

        collectedLabel.text = "WASD to move. Touch a block to collect it. Press " + toggleKey
            + " to open the puzzle. Collected " + PuzzleProgress.Count + " / " + totalPuzzles;
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
        Debug.Log("Puzzle complete. Entering the final level");

        if (puzzlePanel != null)
        {
            PuzzleBoard board = puzzlePanel.GetComponent<PuzzleBoard>();
            if (board != null)
            {
                board.Lock();
            }
        }

        if (solvedMessage != null)
        {
            solvedMessage.SetActive(true);
        }

        if (collectedLabel != null)
        {
            collectedLabel.text = "Puzzle complete. Entering the final level";
        }

        StartCoroutine(LoadNextLevel());
    }

    IEnumerator LoadNextLevel()
    {
        // timeScale 为 0 时 WaitForSeconds 不会走，所以用不受暂停影响的版本
        yield return new WaitForSecondsRealtime(loadDelay);
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
            {
                SceneManager.LoadScene(nextSceneName);
            }
            else
            {
                Debug.LogWarning("PuzzleManager: scene " + nextSceneName + " is not in Build Settings");
                SetPlayerScriptsEnabled(true);
            }

            yield break;
        }

        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextIndex);
        }
        else
        {
            Debug.LogWarning("PuzzleManager: there is no next scene in Build Settings");
        }
    }
}
