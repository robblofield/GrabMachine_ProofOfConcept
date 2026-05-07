using UnityEngine;

public class GrabMachineVerletPOC : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Transform carriage;
    [SerializeField] private Transform ropeStart;
    [SerializeField] private Rigidbody clawBody;
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Carriage Controls")]
    [SerializeField] private float carriageSpeed = 4f;

    [Header("Rope Settings")]
    [SerializeField] private int ropePointCount = 16;
    [SerializeField] private float segmentLength = 0.25f;
    [SerializeField] private int constraintIterations = 8;
    [SerializeField] private float gravityStrength = 1f;

    [Header("Winch Settings")]
    [SerializeField] private float minRopeLength = 1.5f;
    [SerializeField] private float maxRopeLength = 4f;
    [SerializeField] private float winchSpeed = 1.5f;

    [Header("Claw Follow")]
    [SerializeField] private float clawPullStrength = 80f;
    [SerializeField] private float clawDamping = 8f;

    private Vector3[] points;
    private Vector3[] previousPoints;

    private float currentRopeLength;
    private bool lowering;

    private void Start()
    {
        points = new Vector3[ropePointCount];
        previousPoints = new Vector3[ropePointCount];

        currentRopeLength = minRopeLength;

        for (int i = 0; i < ropePointCount; i++)
        {
            Vector3 startPos = ropeStart.position + Vector3.down * segmentLength * i;

            points[i] = startPos;
            previousPoints[i] = startPos;
        }

        lineRenderer.positionCount = ropePointCount;
    }

    private void Update()
    {
        HandleCarriageInput();
        HandleWinchInput();
        DrawRope();
    }

    private void FixedUpdate()
    {
        SimulateRope();
        PullClawToRopeEnd();
    }

    private void HandleCarriageInput()
    {
        Vector3 input = Vector3.zero;

        if (Input.GetKey(KeyCode.W)) input += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) input += Vector3.back;
        if (Input.GetKey(KeyCode.A)) input += Vector3.left;
        if (Input.GetKey(KeyCode.D)) input += Vector3.right;

        carriage.position += input.normalized * carriageSpeed * Time.deltaTime;
    }

    private void HandleWinchInput()
    {
        lowering = Input.GetKey(KeyCode.Space);

        if (lowering)
        {
            currentRopeLength += winchSpeed * Time.deltaTime;
        }
        else
        {
            currentRopeLength -= winchSpeed * Time.deltaTime;
        }

        currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

        segmentLength = currentRopeLength / (ropePointCount - 1);
    }

    private void SimulateRope()
    {
        // Pin the top rope point to the carriage.
        points[0] = ropeStart.position;

        // Verlet integration.
        for (int i = 1; i < ropePointCount; i++)
        {
            Vector3 currentPoint = points[i];
            Vector3 velocity = points[i] - previousPoints[i];

            previousPoints[i] = currentPoint;

            points[i] += velocity;
            points[i] += Physics.gravity * gravityStrength * Time.fixedDeltaTime * Time.fixedDeltaTime;
        }

        // Keep the rope segments at their target length.
        for (int iteration = 0; iteration < constraintIterations; iteration++)
        {
            ApplyConstraints();
        }
    }

    private void ApplyConstraints()
    {
        points[0] = ropeStart.position;

        for (int i = 0; i < ropePointCount - 1; i++)
        {
            Vector3 pointA = points[i];
            Vector3 pointB = points[i + 1];

            Vector3 difference = pointB - pointA;
            float distance = difference.magnitude;
            float error = distance - segmentLength;

            Vector3 correction = difference.normalized * error;

            if (i == 0)
            {
                // First point is pinned, so only move point B.
                points[i + 1] -= correction;
            }
            else
            {
                points[i] += correction * 0.5f;
                points[i + 1] -= correction * 0.5f;
            }
        }
    }

    private void PullClawToRopeEnd()
    {
        Vector3 ropeEnd = points[ropePointCount - 1];

        Vector3 toRopeEnd = ropeEnd - clawBody.position;
        Vector3 pullForce = toRopeEnd * clawPullStrength;

        Vector3 dampingForce = -clawBody.velocity * clawDamping;

        clawBody.AddForce(pullForce + dampingForce, ForceMode.Force);

        // Keep the final rope point visually attached to the claw.
        points[ropePointCount - 1] = clawBody.position;
    }

    private void DrawRope()
    {
        lineRenderer.SetPositions(points);
    }
}