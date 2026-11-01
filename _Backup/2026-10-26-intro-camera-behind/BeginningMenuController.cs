using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("摄像机设置")]
    public Transform cameraTransform;      // 场景里高处那台 Camera
    public float cameraMoveDuration = 2.0f; // 沿贝塞尔飞到玩家身边的时间

    [Tooltip("控制点偏离中点的比例，按起点到终点的距离计算。0 是直线，越大弧线越往侧面鼓。")]
    [Range(0f, 0.5f)]
    public float curveBend = 0.2f;

    [Header("主菜单 UI 设置")]
    public CanvasGroup menuCanvasGroup;    // 挂在主菜单父物体上的 CanvasGroup
    public float fadeDuration = 0.5f;      // 菜单淡出耗时

    CameraController cameraController;
    PlayerController playerController;
    bool isStarting;

    void Awake()
    {
        cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
        {
            // 必须赶在 CameraController.Start 之前占住，否则 3D 相机会先跳到玩家身边。
            cameraController.holdForIntro = true;
            cameraController.SuppressGameplayCameras();

            if (cameraController.player != null)
            {
                playerController = cameraController.player.GetComponent<PlayerController>();
            }
        }

        if (playerController != null)
        {
            playerController.inputEnabled = false;
        }

        if (cameraTransform != null)
        {
            cameraTransform.gameObject.SetActive(true);
            cameraTransform.gameObject.tag = "MainCamera";
        }

        BindStartButton();
    }

    // Start 现在只是一段文字，场景里没有 Button。运行时补上，点它才开始飞。
    void BindStartButton()
    {
        if (menuCanvasGroup == null)
        {
            return;
        }

        Transform start = menuCanvasGroup.transform.Find("Start");
        if (start == null)
        {
            return;
        }

        Button button = start.GetComponent<Button>();
        if (button == null)
        {
            button = start.gameObject.AddComponent<Button>();
        }

        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(OnStartButtonClicked);
    }

    // 绑定到 Start 按钮的 OnClick 事件
    public void OnStartButtonClicked()
    {
        if (isStarting)
        {
            return;
        }

        isStarting = true;
        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
        }

        Vector3 startPos = cameraTransform.position;
        Quaternion flightRotation = cameraTransform.rotation;
        Vector3 initialEnd = GetOrbitEnd();
        Vector3 controlPoint = GetControlPoint(startPos, initialEnd);

        float elapsed = 0f;
        float originalAlpha = menuCanvasGroup != null ? menuCanvasGroup.alpha : 0f;

        // 只要移动或淡出有一个没完成，就继续循环
        while (elapsed < cameraMoveDuration || elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            if (cameraTransform != null && elapsed < cameraMoveDuration)
            {
                float t = cameraMoveDuration > 0f
                    ? Mathf.Clamp01(elapsed / cameraMoveDuration)
                    : 1f;

                // 缓动只改变沿曲线的快慢。位置仍采样下面的二阶贝塞尔。
                float curveT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 endPos = GetOrbitEnd();
                cameraTransform.position = QuadraticBezier(
                    startPos,
                    controlPoint,
                    endPos,
                    curveT
                );
                cameraTransform.rotation = flightRotation;
            }
            else if (cameraTransform != null)
            {
                cameraTransform.position = GetOrbitEnd();
                cameraTransform.rotation = flightRotation;
            }

            if (menuCanvasGroup != null)
            {
                if (elapsed < fadeDuration)
                {
                    float t = fadeDuration > 0f
                        ? Mathf.Clamp01(elapsed / fadeDuration)
                        : 1f;
                    menuCanvasGroup.alpha = Mathf.Lerp(originalAlpha, 0f, t);
                }
                else
                {
                    menuCanvasGroup.alpha = 0f;
                }
            }

            yield return null;
        }

        if (cameraTransform != null)
        {
            cameraTransform.position = GetOrbitEnd();
            cameraTransform.rotation = flightRotation;
        }

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 0f;
            menuCanvasGroup.gameObject.SetActive(false);
        }

        HandOffToGameplayCamera();
    }

    void HandOffToGameplayCamera()
    {
        if (cameraTransform != null)
        {
            cameraTransform.gameObject.tag = "Untagged";
            cameraTransform.gameObject.SetActive(false);
        }

        if (cameraController != null)
        {
            cameraController.TakeOverAfterIntro();
        }

        if (playerController != null)
        {
            playerController.inputEnabled = true;
        }
    }

    // 终点是玩家身边跟拍相机的轨道位置，不是 Camera3D 在场景里摆的坐标。
    Vector3 GetOrbitEnd()
    {
        if (cameraController != null && cameraController.camera3D != null)
        {
            return cameraController.camera3D.GetOrbitPosition();
        }

        return cameraTransform != null
            ? cameraTransform.position
            : Vector3.zero;
    }

    // 二阶贝塞尔：B(t) = (1-t)^2 P0 + 2(1-t)t P1 + t^2 P2。
    // P0 是起点，P2 是终点，P1 是控制点，曲线本身不经过 P1，只朝它鼓出去。
    static Vector3 QuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return (u * u * p0) + (2f * u * t * p1) + (t * t * p2);
    }

    Vector3 GetControlPoint(Vector3 start, Vector3 end)
    {
        Vector3 mid = (start + end) * 0.5f;
        Vector3 chord = end - start;
        float chordLength = chord.magnitude;
        if (chordLength < 0.001f || curveBend <= 0f)
        {
            return mid;
        }

        // 侧向是弦在水平面上的垂线。弦几乎竖直时，改用世界右方向再求一次。
        Vector3 side = Vector3.Cross(chord, Vector3.up);
        if (side.sqrMagnitude < 0.0001f)
        {
            side = Vector3.Cross(chord, Vector3.right);
        }

        if (side.sqrMagnitude < 0.0001f)
        {
            return mid;
        }

        side.Normalize();

        Transform player = cameraController != null ? cameraController.player : null;
        if (player != null && Vector3.Dot(side, player.position - mid) > 0f)
        {
            side = -side;
        }

        return mid + side * (chordLength * curveBend);
    }
}
