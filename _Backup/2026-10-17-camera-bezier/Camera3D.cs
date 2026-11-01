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


    void Start()
    {
        mousePositionLastFrame = Input.mousePosition;
    }
    void LateUpdate()
    {
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
        Vector3 offset = new Vector3(
            armLength *
            Mathf.Sin(currentRotationAngleY * Mathf.Deg2Rad) *
            Mathf.Sin(currentRotationAngleX * Mathf.Deg2Rad),
            armLength *
            Mathf.Cos(currentRotationAngleY * Mathf.Deg2Rad),
            armLength *
            Mathf.Sin(currentRotationAngleY * Mathf.Deg2Rad) *
            Mathf.Cos(currentRotationAngleX * Mathf.Deg2Rad)
        );
        transform.position =
            player.position + offset;
        transform.LookAt(player);
    }
}