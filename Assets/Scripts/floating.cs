using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class floating : MonoBehaviour
{
    [Header("上下浮动")]
    public float floatHeight = 0.15f;
    public float floatSpeed = 1.5f;

    [Header("旋转")]
    public float rotationSpeed = 60f; 

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float newY = startPosition.y
                   + Mathf.Sin(Time.time * floatSpeed) * floatHeight;

        transform.position = new Vector3(
            startPosition.x,
            newY,
            startPosition.z
        );

        transform.Rotate(
            0f,
            rotationSpeed * Time.deltaTime,
            0f,
            Space.World
        );
    }
}
