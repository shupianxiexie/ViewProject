using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [Header("摄像机设置")]
    public Transform cameraTransform;      // 主摄像机的 Transform
    public Transform targetPosition;       // 摄像机下移后的目标点（场景中空物体）
    public float cameraMoveDuration = 2.0f; // 摄像机下移耗时

    [Header("主菜单 UI 设置")]
    public CanvasGroup menuCanvasGroup;    // 挂在主菜单父物体上的 CanvasGroup
    public float fadeDuration = 0.5f;      // 菜单淡出耗时

    private bool isStarting = false;

    // 绑定到 Start 按钮的 OnClick 事件
    public void OnStartButtonClicked()
    {
        if (isStarting) return; // 防止重复点击
        isStarting = true;

        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        // 淡出期间禁用菜单交互，防止误触
        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        Vector3 startPos = cameraTransform.position;
        Vector3 endPos = targetPosition.position;
        float elapsed = 0f;
        float originalAlpha = menuCanvasGroup.alpha;

        // 只要移动或淡出有一个没完成，就继续循环
        while (elapsed < cameraMoveDuration || elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;

            // 摄像机移动（SmoothStep 让起步和停止更自然）
            if (elapsed < cameraMoveDuration)
            {
                float t = Mathf.Clamp01(elapsed / cameraMoveDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);
                cameraTransform.position = Vector3.Lerp(startPos, endPos, smoothT);
            }
            else
            {
                cameraTransform.position = endPos;
            }

            // UI 淡出
            if (elapsed < fadeDuration)
            {
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                menuCanvasGroup.alpha = Mathf.Lerp(originalAlpha, 0f, t);
            }
            else
            {
                menuCanvasGroup.alpha = 0f;
            }

            yield return null;
        }

        // 收尾，确保数值精确
        cameraTransform.position = endPos;
        menuCanvasGroup.alpha = 0f;

        // 失活主菜单父物体（只关闭这一组 UI，不影响其他 UI）
        menuCanvasGroup.gameObject.SetActive(false);

        // 在这里启用你的游戏逻辑，例如：
        // GameManager.Instance.StartGameplay();
    }
}