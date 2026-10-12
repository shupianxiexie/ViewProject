using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class Camera2DX : MonoBehaviour
{
    public float orthographicSize = 15f;

    public Transform player;


    void OnEnable()
    {
        Camera cameraComponent = GetComponent<Camera>();

        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = orthographicSize;

        transform.rotation = Quaternion.Euler(0f, -90f, 0f);
    }


    void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        transform.position = new Vector3(
            LevelConfig.instance.cameraPosition2DX,
            player.position.y + 1f,
            player.position.z
        );
    }
}