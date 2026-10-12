using UnityEngine;

public class GrassSway : MonoBehaviour
{
    [Header("摆动设置")]
    public float swayAngle = 8f;      // 最大摆动角度
    public float swaySpeed = 2f;      // 摆动速度
    public float randomOffset = 2f;   // 每株草的随机速度偏移

    private Quaternion startRotation;
    private float offset;

    void Start()
    {
        startRotation = transform.localRotation;
        offset = Random.Range(0f, 10f);
        swaySpeed += Random.Range(-randomOffset, randomOffset);
        swaySpeed = Mathf.Max(0.1f, swaySpeed);
    }

    void Update()
    {
        float angle = Mathf.Sin(Time.time * swaySpeed + offset) * swayAngle;

        transform.localRotation =
            startRotation * Quaternion.Euler(angle, 0f, 0f);
    }
}