using System.Collections;
using System.Diagnostics;
using UnityEngine;

public class CameraTransition : MonoBehaviour
{
    [Header("摄像机引用")]
    public Camera camera3D;
    public Camera camera2DX;
    public Camera camera2DZ;

    [Header("过渡设置")]
    public float transitionDuration = 0.8f;
    public float lookDistance = 10f;

    public AnimationCurve transitionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool IsTransitioning { get; private set; }

    private Camera currentCamera;
    private Coroutine transitionCoroutine;

    private void Start()
    {
        // 根据当前启用状态确定初始摄像机
        if (camera3D != null && camera3D.enabled)
            currentCamera = camera3D;
        else if (camera2DX != null && camera2DX.enabled)
            currentCamera = camera2DX;
        else if (camera2DZ != null && camera2DZ.enabled)
            currentCamera = camera2DZ;
    }

    public void SwitchTo3D()
    {
        SwitchTo(camera3D);
    }

    public void SwitchTo2DX()
    {
        SwitchTo(camera2DX);
    }

    public void SwitchTo2DZ()
    {
        SwitchTo(camera2DZ);
    }

    public void SwitchTo(Camera targetCamera)
    {
        if (IsTransitioning)
            return;

        if (currentCamera == null)
        {
            currentCamera = targetCamera;
            currentCamera.enabled = true;
            return;
        }

        if (currentCamera == targetCamera)
            return;

        transitionCoroutine = StartCoroutine(
            TransitionCamera(currentCamera, targetCamera)
        );
    }

    private IEnumerator TransitionCamera(
        Camera source,
        Camera target)
    {
        IsTransitioning = true;

        Vector3 startPosition = source.transform.position;
        Vector3 targetPosition = target.transform.position;

        // 固定记录切换开始时原摄像机的注视点
        Vector3 lookTarget =
            startPosition + source.transform.forward * lookDistance;

        // 保存目标摄像机原本的朝向。
        // 该值在当前版本中暂不用于最终旋转。
        Quaternion targetOriginalRotation = target.transform.rotation;

        source.enabled = true;
        target.enabled = false;

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;

            float t = transitionDuration > 0f
                ? Mathf.Clamp01(elapsedTime / transitionDuration)
                : 1f;

            float curveT = transitionCurve.Evaluate(t);

            source.transform.position = Vector3.Lerp(
                startPosition,
                targetPosition,
                curveT
            );

            Vector3 direction =
                lookTarget - source.transform.position;

            if (direction.sqrMagnitude > 0.0001f)
            {
                source.transform.rotation =
                    Quaternion.LookRotation(direction, Vector3.up);
            }

            yield return null;
        }

        source.transform.position = targetPosition;

        Vector3 finalDirection =
            lookTarget - source.transform.position;

        if (finalDirection.sqrMagnitude > 0.0001f)
        {
            source.transform.rotation =
                Quaternion.LookRotation(finalDirection, Vector3.up);
        }

        // 同步最终位置和旋转，避免启用目标时画面跳变
        target.transform.position = source.transform.position;
        target.transform.rotation = source.transform.rotation;

        source.enabled = false;
        target.enabled = true;

        currentCamera = target;

        IsTransitioning = false;
        transitionCoroutine = null;
    }
}