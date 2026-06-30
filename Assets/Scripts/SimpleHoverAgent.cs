using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class SimpleHoverAgent : Agent
{
    public Transform[] waypoints;
    private int currentWaypoint = 0;

    private Vector3 startPos;
    private Quaternion startRot;
    private Rigidbody rb;

    public float moveSpeed = 25f;
    public float turnSpeed = 150f;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        startPos = transform.position;
        startRot = transform.rotation;
    }

    public override void OnEpisodeBegin()
    {
        transform.position = startPos;
        transform.rotation = startRot;
        rb.linearVelocity = Vector3.zero;
        currentWaypoint = 0;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Where is the waypoint? (X and Z direction)
        Vector3 toWaypoint = waypoints[currentWaypoint].position - transform.position;
        sensor.AddObservation(transform.InverseTransformDirection(toWaypoint.normalized));

        // 2. How far away is it?
        sensor.AddObservation(Vector3.Distance(transform.position, waypoints[currentWaypoint].position) / 50f);

        // Total Observations = 4
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float gas = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f); // 0 to 1 only

        // Pure, flawless movement (no wheel slip)
        transform.Rotate(0, steering * turnSpeed * Time.fixedDeltaTime, 0);
        transform.Translate(Vector3.forward * gas * moveSpeed * Time.fixedDeltaTime);

        // Waypoint Logic
        float dist = Vector3.Distance(transform.position, waypoints[currentWaypoint].position);
        if (dist < 12f)
        {
            AddReward(1.0f); // +1 for hitting waypoint
            currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
        }

        // Tiny time penalty so it doesn't sit still
        AddReward(-0.001f);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-1.0f); // -1 penalty. Scared enough to avoid, brave enough to drive.
            EndEpisode();
        }
    }

    // This lets you drive the box yourself to test it!
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Input.GetAxis("Vertical");
    }
}