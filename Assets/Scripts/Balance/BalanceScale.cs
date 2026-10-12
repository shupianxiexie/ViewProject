using UnityEngine;

public class BalanceScale : MonoBehaviour
{
    [Header("References")]
    public Transform beam;
    public Transform leftPan;
    public Transform rightPan;

    [Header("Tilt Settings")]
    public float maxTiltAngle = 30f;
    public float anglePerWeightUnit = 15f;
    public float tiltSpeed = 5f;
    public bool autoCalculateRotationAxis = true;
    public Vector3 rotationAxis = Vector3.forward;

    private float leftWeight;
    private float rightWeight;
    private float currentTiltAngle;
    private bool initialPoseCaptured;
    private Vector3 pivotLocalPosition;
    private Quaternion initialBeamRotation;
    private Vector3 initialLeftPanPosition;
    private Vector3 initialRightPanPosition;
    private Quaternion initialLeftPanRotation;
    private Quaternion initialRightPanRotation;

    public float LeftWeight => leftWeight;
    public float RightWeight => rightWeight;

    void Start()
    {
        ResolveReferences();
        CaptureInitialPose();
    }

    void Update()
    {
        if (!initialPoseCaptured)
        {
            ResolveReferences();
            CaptureInitialPose();
        }

        if (!initialPoseCaptured)
        {
            return;
        }

        // Positive angle lowers the left side; negative angle lowers the right side.
        float weightDifference = leftWeight - rightWeight;
        float targetTiltAngle = Mathf.Clamp(
            weightDifference * anglePerWeightUnit,
            -maxTiltAngle,
            maxTiltAngle
        );

        currentTiltAngle = Mathf.Lerp(
            currentTiltAngle,
            targetTiltAngle,
            tiltSpeed * Time.deltaTime
        );

        Vector3 tiltAxis = GetTiltAxis();
        Quaternion tiltRotation = Quaternion.AngleAxis(currentTiltAngle, tiltAxis);

        beam.rotation = transform.rotation * tiltRotation * initialBeamRotation;
        UpdatePanPose(leftPan, initialLeftPanPosition, initialLeftPanRotation, tiltRotation);
        UpdatePanPose(rightPan, initialRightPanPosition, initialRightPanRotation, tiltRotation);
    }

    void ResolveReferences()
    {
        if (leftPan == null || rightPan == null)
        {
            BalancePan[] pans = GetComponentsInChildren<BalancePan>(true);
            foreach (BalancePan pan in pans)
            {
                if (pan == null)
                {
                    continue;
                }

                if (leftPan == null && pan.side == BalancePan.PanSide.Left)
                {
                    leftPan = pan.transform;
                }
                else if (rightPan == null && pan.side == BalancePan.PanSide.Right)
                {
                    rightPan = pan.transform;
                }
            }
        }

        if (beam != null)
        {
            return;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == leftPan || child == rightPan)
            {
                continue;
            }

            if (child.GetComponent<BalancePan>() != null)
            {
                continue;
            }

            beam = child;
            break;
        }
    }

    void CaptureInitialPose()
    {
        if (beam == null || leftPan == null || rightPan == null)
        {
            return;
        }

        pivotLocalPosition = transform.InverseTransformPoint(beam.position);
        initialBeamRotation = Quaternion.Inverse(transform.rotation) * beam.rotation;

        initialLeftPanPosition = transform.InverseTransformPoint(leftPan.position);
        initialRightPanPosition = transform.InverseTransformPoint(rightPan.position);
        initialLeftPanRotation = Quaternion.Inverse(transform.rotation) * leftPan.rotation;
        initialRightPanRotation = Quaternion.Inverse(transform.rotation) * rightPan.rotation;
        initialPoseCaptured = true;
    }

    Vector3 GetTiltAxis()
    {
        if (autoCalculateRotationAxis)
        {
            Vector3 panDirection = initialRightPanPosition - initialLeftPanPosition;
            Vector3 calculatedAxis = Vector3.Cross(panDirection.normalized, Vector3.up);
            if (calculatedAxis.sqrMagnitude > 0.0001f)
            {
                return calculatedAxis.normalized;
            }
        }

        return rotationAxis.sqrMagnitude > 0.0001f
            ? rotationAxis.normalized
            : Vector3.forward;
    }

    void UpdatePanPose(
        Transform pan,
        Vector3 initialPosition,
        Quaternion initialRotation,
        Quaternion tiltRotation)
    {
        Vector3 offsetFromPivot = initialPosition - pivotLocalPosition;
        Vector3 tiltedPosition = pivotLocalPosition + tiltRotation * offsetFromPivot;

        pan.position = transform.TransformPoint(tiltedPosition);

        // The pan follows the end of the beam but remains level.
        pan.rotation = transform.rotation * initialRotation;
    }

    public void RegisterWeight(BalancePan.PanSide side, float weight)
    {
        AdjustWeight(side, weight);
    }

    public void UnregisterWeight(BalancePan.PanSide side, float weight)
    {
        AdjustWeight(side, -weight);
    }

    public void AdjustWeight(BalancePan.PanSide side, float amount)
    {
        if (side == BalancePan.PanSide.Left)
        {
            leftWeight = Mathf.Max(0f, leftWeight + amount);
        }
        else
        {
            rightWeight = Mathf.Max(0f, rightWeight + amount);
        }
    }
}
