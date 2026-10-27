using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("摄像机设置")]
    public Transform cameraTransform;      // 场景里高处那台 Camera，只负责菜单画面
    public float cameraMoveDuration = 2.0f; // 点开始后，跟拍相机从高处飞到角色背后的时间

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
            // 必须赶在 CameraController.Start 之前占住，否则跟拍会先跳到轨道上，高处菜单画面出不来。
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

        if (cameraTransform == null)
        {
            FinishIntro();
            yield break;
        }

        Vector3 startPos = cameraTransform.position;
        Quaternion startRot = cameraTransform.rotation;
        Transform flight = cameraTransform;

        // 先关掉高处那台，再打开跟拍相机，避免两台同时响。
        // 跟拍相机摆在同一位置、同一朝向，这一下画面不跳。之后只移动它，飞完不再换相机。
        if (cameraController != null && cameraController.camera3D != null)
        {
            cameraTransform.gameObject.tag = "Untagged";
            cameraTransform.gameObject.SetActive(false);
            cameraController.UseGameplayCameraForIntroFlight(startPos, startRot);
            flight = cameraController.camera3D.transform;
        }

        float elapsed = 0f;
        float originalAlpha = menuCanvasGroup != null ? menuCanvasGroup.alpha : 0f;

        // 只要移动或淡出有一个没完成，就继续循环
        while (elapsed < cameraMoveDuration || elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            if (flight != null && elapsed < cameraMoveDuration)
            {
                float t = cameraMoveDuration > 0f
                    ? Mathf.Clamp01(elapsed / cameraMoveDuration)
                    : 1f;

                // 缓动只改变快慢。路径是高处到角色背后的直线，不往侧面偏。
                float curveT = Mathf.SmoothStep(0f, 1f, t);
                Vector3 endPos = GetBehindPosition();
                flight.position = Vector3.Lerp(startPos, endPos, curveT);
                flight.rotation = Quaternion.Slerp(
                    startRot,
                    LookTowardPlayer(flight.position, startRot),
                    curveT
                );
            }
            else if (flight != null)
            {
                Vector3 endPos = GetBehindPosition();
                flight.position = endPos;
                flight.rotation = LookTowardPlayer(endPos, startRot);
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

        if (flight != null)
        {
            Vector3 endPos = GetBehindPosition();
            flight.position = endPos;
            FacePlayer(flight);
        }

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha = 0f;
            menuCanvasGroup.gameObject.SetActive(false);
        }

        FinishIntro();
    }

    void FinishIntro()
    {
        if (cameraController != null)
        {
            cameraController.FinishIntroFlight();
        }

        if (playerController != null)
        {
            playerController.inputEnabled = true;
        }
    }

    // 终点是角色背后的跟拍轨道。水平角在点击开始时已经改到背后，高度和距离仍是 Camera3D 上的俯仰角和臂长。
    Vector3 GetBehindPosition()
    {
        if (cameraController != null && cameraController.camera3D != null)
        {
            return cameraController.camera3D.GetOrbitPosition();
        }

        return cameraTransform != null
            ? cameraTransform.position
            : Vector3.zero;
    }

    Quaternion LookTowardPlayer(Vector3 fromPosition, Quaternion fallback)
    {
        Transform target = cameraController != null ? cameraController.player : null;
        if (target == null)
        {
            return fallback;
        }

        Vector3 toPlayer = target.position - fromPosition;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            return fallback;
        }

        // 几乎在角色正上方时，世界上方向和视线平行，旋转会失效。
        Vector3 up = Vector3.up;
        if (Mathf.Abs(Vector3.Dot(toPlayer.normalized, Vector3.up)) > 0.99f)
        {
            up = Vector3.forward;
        }

        return Quaternion.LookRotation(toPlayer, up);
    }

    void FacePlayer(Transform flight)
    {
        Transform target = cameraController != null ? cameraController.player : null;
        if (flight == null || target == null)
        {
            return;
        }

        Vector3 toPlayer = target.position - flight.position;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            return;
        }

        flight.rotation = LookTowardPlayer(flight.position, flight.rotation);
    }
}
