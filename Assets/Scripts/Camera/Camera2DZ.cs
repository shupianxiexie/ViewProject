using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Camera2DZ : MonoBehaviour
{
    public float orthographicSize = 15f;

    public Transform player;

    // 切换过渡时由 CameraController 写位置。这里继续跟拍的话，相机会被拉回正视轴。
    [HideInInspector]
    public bool suspendFollow;


    void OnEnable()
    {
        Camera cameraComponent = GetComponent<Camera>();

        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = orthographicSize;

        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
    }


    void LateUpdate()
    {
        if (suspendFollow || player == null)
        {
            return;
        }

        transform.position = GetFollowPosition();
    }

    // 正视机位。CameraController 用同一个点当贝塞尔终点。
    public Vector3 GetFollowPosition()
    {
        if (player == null || LevelConfig.instance == null)
        {
            return transform.position;
        }

        return new Vector3(
            player.position.x,
            player.position.y + 1f,
            LevelConfig.instance.cameraPosition2DZ
        );
    }
}