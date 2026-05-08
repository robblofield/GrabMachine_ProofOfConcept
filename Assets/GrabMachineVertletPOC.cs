using UnityEngine;

// This script is a very basic proof-of-concept for a grab machine rope.
// The carriage is moved with WASD.
// Holding Space lowers the rope.
// Releasing Space raises the rope.
// The rope itself is simulated using a simple Verlet-style point system.
public class GrabMachineVerletPOC : MonoBehaviour
{
    [Header("Scene References")]

    // The moving top part of the grab machine.
    [SerializeField] private Transform carriage;

    // The point where the rope begins.
    // This should usually be an empty GameObject placed under the carriage.
    [SerializeField] private Transform ropeStart;

    // The physical claw object.
    // This should have a Rigidbody attached so Unity physics can move it.
    [SerializeField] private Rigidbody clawBody;

    // Draws the visible rope between the rope points.
    [SerializeField] private LineRenderer lineRenderer;

    [Header("Carriage Controls")]

    // How fast the carriage moves when using WASD.
    [SerializeField] private float carriageSpeed = 4f;

    [Header("Rope Settings")]

    // How many points make up the simulated rope.
    // More points means a smoother rope, but slightly more calculation.
    [SerializeField] private int ropePointCount = 16;

    // The target distance between each rope point.
    // This changes when the rope is raised or lowered.
    [SerializeField] private float segmentLength = 0.25f;

    // How many times we correct the rope each physics frame.
    // Higher values make the rope stiffer and less stretchy.
    [SerializeField] private int constraintIterations = 8;

    // Multiplier for gravity in the rope simulation.
    [SerializeField] private float gravityStrength = 1f;

    [Header("Winch Settings")]

    // The shortest allowed rope length.
    [SerializeField] private float minRopeLength = 1.5f;

    // The longest allowed rope length.
    [SerializeField] private float maxRopeLength = 4f;

    // How quickly the rope length changes when raising or lowering.
    [SerializeField] private float winchSpeed = 1.5f;

    [Header("Claw Follow")]

    // How strongly the claw is pulled toward the end of the rope.
    [SerializeField] private float clawPullStrength = 80f;

    // How much the claw movement is damped.
    // Higher values reduce swinging and help the claw settle.
    [SerializeField] private float clawDamping = 8f;

    // The current positions of each point in the rope.
    private Vector3[] points;

    // The previous positions of each rope point.
    // Verlet integration uses the difference between current and previous position
    // to estimate velocity without storing a separate velocity variable.
    private Vector3[] previousPoints;

    // The current full length of the rope.
    private float currentRopeLength;

    // Whether the player is currently holding the lower button.
    private bool lowering;

    private void Start()
    {
        // Create the array that stores the rope's current point positions.
        points = new Vector3[ropePointCount];

        // Create the array that stores the rope's previous point positions.
        previousPoints = new Vector3[ropePointCount];

        // Start with the rope at its shortest length.
        currentRopeLength = minRopeLength;

        // Set up the starting positions for each rope point.
        for (int i = 0; i < ropePointCount; i++)
        {
            // Place each point below the rope start.
            // Point 0 is at the top.
            // Each next point is one segment lower.
            Vector3 startPos = ropeStart.position + Vector3.down * segmentLength * i;

            // Store this as the current position.
            points[i] = startPos;

            // Store the same value as the previous position.
            // This means the rope starts with no initial velocity.
            previousPoints[i] = startPos;
        }

        // Tell the LineRenderer how many positions it needs to draw.
        lineRenderer.positionCount = ropePointCount;
    }

    private void Update()
    {
        // Read player input for moving the carriage.
        HandleCarriageInput();

        // Read player input for raising/lowering the rope.
        HandleWinchInput();

        // Draw the rope every frame so the visuals stay responsive.
        DrawRope();
    }

    private void FixedUpdate()
    {
        // Simulate the rope during the physics update.
        SimulateRope();

        // Pull the Rigidbody claw toward the final rope point.
        PullClawToRopeEnd();
    }

    private void HandleCarriageInput()
    {
        // Start with no movement input.
        Vector3 input = Vector3.zero;

        // Add forward input when W is held.
        if (Input.GetKey(KeyCode.W)) input += Vector3.forward;

        // Add backward input when S is held.
        if (Input.GetKey(KeyCode.S)) input += Vector3.back;

        // Add left input when A is held.
        if (Input.GetKey(KeyCode.A)) input += Vector3.left;

        // Add right input when D is held.
        if (Input.GetKey(KeyCode.D)) input += Vector3.right;

        // Move the carriage using the input direction.
        // normalized prevents diagonal movement being faster.
        // Time.deltaTime keeps movement frame-rate independent.
        carriage.position += input.normalized * carriageSpeed * Time.deltaTime;
    }

    private void HandleWinchInput()
    {
        // Space controls whether the rope is being lowered.
        lowering = Input.GetKey(KeyCode.Space);

        // If Space is held, increase the rope length.
        if (lowering)
        {
            currentRopeLength += winchSpeed * Time.deltaTime;
        }
        // If Space is not held, decrease the rope length.
        else
        {
            currentRopeLength -= winchSpeed * Time.deltaTime;
        }

        // Prevent the rope from becoming shorter or longer than allowed.
        currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

        // Recalculate the length of each rope segment.
        // There are ropePointCount points, but one fewer gaps between them.
        segmentLength = currentRopeLength / (ropePointCount - 1);
    }

    private void SimulateRope()
    {
        // Lock the first rope point to the rope start position.
        // This makes the rope follow the carriage at the top.
        points[0] = ropeStart.position;

        // Move each rope point using Verlet integration.
        // Start from 1 because point 0 is pinned to the carriage.
        for (int i = 1; i < ropePointCount; i++)
        {
            // Store the current point position before changing it.
            Vector3 currentPoint = points[i];

            // Estimate velocity from the difference between this frame's position
            // and the previous frame's position.
            Vector3 velocity = points[i] - previousPoints[i];

            // Save the current position as the previous position for next time.
            previousPoints[i] = currentPoint;

            // Move the point by its estimated velocity.
            points[i] += velocity;

            // Apply gravity to the point.
            // The fixedDeltaTime squared part is standard for position-based integration.
            points[i] += Physics.gravity * gravityStrength * Time.fixedDeltaTime * Time.fixedDeltaTime;
        }

        // Repeatedly correct the rope so neighbouring points stay the right distance apart.
        // More iterations produce a tighter, less stretchy rope.
        for (int iteration = 0; iteration < constraintIterations; iteration++)
        {
            ApplyConstraints();
        }
    }

    private void ApplyConstraints()
    {
        // Keep the first point locked to the rope start.
        // This is repeated because constraint solving may move it otherwise.
        points[0] = ropeStart.position;

        // Loop through each pair of connected rope points.
        for (int i = 0; i < ropePointCount - 1; i++)
        {
            // First point in this rope segment.
            Vector3 pointA = points[i];

            // Second point in this rope segment.
            Vector3 pointB = points[i + 1];

            // Direction and distance from point A to point B.
            Vector3 difference = pointB - pointA;

            // Current distance between the two points.
            float distance = difference.magnitude;

            // How far away this distance is from the target segment length.
            // Positive means the segment is too long.
            // Negative means the segment is too short.
            float error = distance - segmentLength;

            // The correction needed to bring the segment back toward the target length.
            Vector3 correction = difference.normalized * error;

            // If this is the first segment, point A is pinned.
            if (i == 0)
            {
                // Only move point B because point A must stay attached to the carriage.
                points[i + 1] -= correction;
            }
            else
            {
                // For normal rope segments, split the correction between both points.
                points[i] += correction * 0.5f;
                points[i + 1] -= correction * 0.5f;
            }
        }
    }

    private void PullClawToRopeEnd()
    {
        // Get the final point of the simulated rope.
        Vector3 ropeEnd = points[ropePointCount - 1];

        // Work out the direction and distance from the claw to the rope end.
        Vector3 toRopeEnd = ropeEnd - clawBody.position;

        // Convert that offset into a pulling force.
        // The further the claw is from the rope end, the stronger the pull.
        Vector3 pullForce = toRopeEnd * clawPullStrength;

        // Apply damping opposite to the claw's current velocity.
        // This reduces endless bouncing and swinging.
        Vector3 dampingForce = -clawBody.velocity * clawDamping;

        // Apply the combined pull and damping force to the claw Rigidbody.
        clawBody.AddForce(pullForce + dampingForce, ForceMode.Force);

        // Snap the final rope point to the claw position for visual attachment.
        // This makes the drawn rope appear connected to the physical claw.
        points[ropePointCount - 1] = clawBody.position;
    }

    private void DrawRope()
    {
        // Give the rope point positions to the LineRenderer.
        // This updates the visible rope in the scene.
        lineRenderer.SetPositions(points);
    }
}