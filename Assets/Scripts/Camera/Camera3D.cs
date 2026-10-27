using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Threading;
using UnityEngine;

public class Camera3D : MonoBehaviour
{
    public Transform player;

    public float rotationSpeed = 90.0f;

    public float armLength = 10.0f;

    public float minYRotation = -90.0f;
    public float maxYRotation = 90.0f;

    public float currentRotationAngleX = 90f;
    public float currentRotationAngleY = 90f;

    private Vector2 mousePositionLastFrame;

    // 开场飞入和视角切换时由外面写位置。这里继续转的话，飞行动线会被盖掉。
    [HideInInspector]
    public bool suspendFollow;


    void Start()
    {
        mousePositionLastFrame = Input.mousePosition;
    }
    void LateUpdate()
    {
        // 鼠标基准点仍要刷新，否则过渡结束那一帧会把这段时间的鼠标位移一次转完。
        if (suspendFollow)
        {
            mousePositionLastFrame = Input.mousePosition;
            return;
        }

        RotateCamera();
    }
    void RotateCamera()
    {
        Vector2 mousePosition = Input.mousePosition;
        Vector2 mouseDirection =
            mousePosition - mousePositionLastFrame;
        mousePositionLastFrame = mousePosition;
        if (CarryLight.HeldLight != null)
        {
            mouseDirection = Vector2.zero;
        }
        float rotationAmountX =
            mouseDirection.x *
            rotationSpeed *
            Time.deltaTime;
        float rotationAmountY =
            mouseDirection.y *
            rotationSpeed *
            Time.deltaTime;
        currentRotationAngleX += rotationAmountX;
        currentRotationAngleY += rotationAmountY;
        currentRotationAngleY =
            Mathf.Clamp(
                currentRotationAngleY,
                minYRotation,
                maxYRotation
            );
        transform.position = GetOrbitPosition();
        if (player != null)
        {
            transform.LookAt(player);
        }
    }

    // 过渡飞回 3D 时的终点。必须和上面这套臂长、角度一致，否则交接时会跳一下。
    public Vector3 GetOrbitPosition()
    {
        if (player == null)
        {
            return transform.position;
        }

        float yaw = currentRotationAngleX * Mathf.Deg2Rad;
        float pitch = currentRotationAngleY * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(
            armLength * Mathf.Sin(pitch) * Mathf.Sin(yaw),
            armLength * Mathf.Cos(pitch),
            armLength * Mathf.Sin(pitch) * Mathf.Cos(yaw)
        );
        return player.position + offset;
    }

    // 水平角改到角色正后方。俯仰角和臂长不动：俯仰角 90 是与角色同高，调小会抬高；臂长是距离。
    public void PlaceOrbitBehindPlayer()
    {
        Vector3 back = Vector3.back;
        if (player != null)
        {
            back = -player.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.0001f)
            {
                back = Vector3.back;
            }
            else
            {
                back.Normalize();
            }
        }

        currentRotationAngleX = Mathf.Atan2(back.x, back.z) * Mathf.Rad2Deg;
    }
}