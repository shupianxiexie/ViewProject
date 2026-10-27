using UnityEngine;

// ============================================================================
// PuzzleTestWalker —— 只给拼图测试场景用的简易走动
// ----------------------------------------------------------------------------
// 正式关卡用的是 PlayerController，它还依赖镜头切换。
// 测试场景不需要那些，所以这里只用 WASD 在平面上走，方便去碰碎片。
// ============================================================================
[RequireComponent(typeof(Rigidbody))]
public class PuzzleTestWalker : MonoBehaviour
{
    public float speed = 7f;

    Rigidbody body;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    void Update()
    {
        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;

        Vector3 move = new Vector3(x, 0f, z);
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        Vector3 velocity = body.velocity;
        velocity.x = move.x * speed;
        velocity.z = move.z * speed;
        body.velocity = velocity;

        // 走出地面掉下去时拉回来，避免测试时要重开
        if (transform.position.y < -4f)
        {
            body.position = new Vector3(0f, 2f, 0f);
            body.velocity = Vector3.zero;
        }
    }

    void OnDisable()
    {
        if (body == null)
        {
            return;
        }

        Vector3 velocity = body.velocity;
        velocity.x = 0f;
        velocity.z = 0f;
        body.velocity = velocity;
    }
}
