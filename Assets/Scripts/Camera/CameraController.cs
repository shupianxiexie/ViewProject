using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    public Camera3D camera3D;
    public Camera2DX camera2DX;
    public Camera2DZ camera2DZ;
    public PlayerController playerController;

    public Transform player;

    private Rigidbody playerRigidbody;

    public View.State currentState = View.State.Version3D;

    private bool isTransitioning = false;

    View playerView;

    void Awake()
    {
        if (player != null)
        {
            playerView = player.GetComponent<View>();
            playerRigidbody = player.GetComponent<Rigidbody>();
        }
    }

    void Start()
    {
        ChangeCamera(View.State.Version3D);
    }

    void LateUpdate()
    {
        if (playerView == null && player != null)
        {
            playerView = player.GetComponent<View>();
        }

        if (playerView != null && playerView.currentState != currentState && !isTransitioning)
        {
            ChangeCamera(playerView.currentState);
        }
    }

    public void ChangeCamera(View.State state)
    {
        currentState = state;

        camera3D.gameObject.SetActive(false);
        camera2DX.gameObject.SetActive(false);
        camera2DZ.gameObject.SetActive(false);

        switch (state)
        {
            case View.State.Version3D:
                camera3D.gameObject.SetActive(true);
                playerRigidbody.constraints =
                    RigidbodyConstraints.FreezeRotationX |
                    RigidbodyConstraints.FreezeRotationY |
                    RigidbodyConstraints.FreezeRotationZ;
                break;

            case View.State.Version2DX:
                camera2DX.gameObject.SetActive(true);
                playerRigidbody.constraints =
                    RigidbodyConstraints.FreezePositionX |
                    RigidbodyConstraints.FreezeRotationX |
                    RigidbodyConstraints.FreezeRotationY |
                    RigidbodyConstraints.FreezeRotationZ;
                break;

            case View.State.Version2DZ:
                camera2DZ.gameObject.SetActive(true);
                playerRigidbody.constraints =
                    RigidbodyConstraints.FreezePositionZ |
                    RigidbodyConstraints.FreezeRotationX |
                    RigidbodyConstraints.FreezeRotationY |
                    RigidbodyConstraints.FreezeRotationZ;
                break;
        }
    }
}
