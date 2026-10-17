using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class Camera2DX : MonoBehaviour
{
    public float orthographicSize = 15f;

    public Transform player;

    // 切换过渡时由 CameraController 写位置。这里继续跟拍的话，相机会被拉回侧视轴。
    [HideInInspector]
    public bool suspendFollow;


    void OnEnable()
    {
        Camera cameraComponent = GetComponent<Camera>();

        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = orthographicSize;

        transform.rotation = Quaternion.Euler(0f, -90f, 0f);
    }


    void LateUpdate()
    {
        if (suspendFollow || player == null)
        {
            return;
        }

        transform.position = GetFollowPosition();
    }

    // 侧视机位。CameraController 用同一个点当贝塞尔终点。
    public Vector3 GetFollowPosition()
    {
        if (player == null || LevelConfig.instance == null)
        {
            return transform.position;
        }

        return new Vector3(
            LevelConfig.instance.cameraPosition2DX,
            player.position.y + 1f,
            player.position.z
        );
    }
}