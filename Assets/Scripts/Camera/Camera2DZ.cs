using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Camera2DZ : MonoBehaviour
{
    public float orthographicSize = 15f;

    public Transform player;


    void OnEnable()
    {
        Camera cameraComponent = GetComponent<Camera>();

        cameraComponent.orthographic = true;
        cameraComponent.orthographicSize = orthographicSize;

        transform.rotation = Quaternion.Euler(0f, 0f, 0f);
    }


    void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        transform.position = new Vector3(
            player.position.x,
            player.position.y + 1f,
            LevelConfig.instance.cameraPosition2DZ
        );
    }
}