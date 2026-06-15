// AITrainingDriver.cs
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(ProCarController))]
public class AITrainingDriver : Agent
{
    [Header("Waypoint System")]
    public Transform[] waypoints;
    public float waypointReachRadius = 8f;

    [Header("Training Settings")]
    public float maxEpisodeTime = 60f;
    public float minSpeedThreshold = 2f; // KMH - punish if stuck
    public float stuckCheckInterval = 5f;

    private ProCarController car;
    private Rigidbody rb;
    private Vector3 startPosition;
    private Quaternion startRotation;

    private int currentWaypointIndex = 0;
    private float episodeTimer = 0f;
    private float stuckTimer = 0f;
    private Vector3 lastPosition;
    private int totalWaypointsReached = 0;

    public override void Initialize()
    {
        car = GetComponent<ProCarController>();
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Speed (normalized)
        sensor.AddObservation(Mathf.Clamp01(car.KMPH / 200f));

        // 2. Direction to next waypoint (local space)
        if (waypoints != null && waypoints.Length > 0)
        {
            Vector3 toWaypoint = waypoints[currentWaypointIndex].position - transform.position;
            Vector3 localDir = transform.InverseTransformDirection(toWaypoint.normalized);
            sensor.AddObservation(localDir.x); // left/right
            sensor.AddObservation(localDir.z); // forward/back

            // Distance to waypoint (normalized)
            float dist = toWaypoint.magnitude;
            sensor.AddObservation(Mathf.Clamp01(dist / 50f));
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }

        // 3. Car's forward vs velocity alignment (are we drifting badly?)
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        sensor.AddObservation(Mathf.Clamp(localVelocity.x / 30f, -1f, 1f)); // sideways slip
        sensor.AddObservation(Mathf.Clamp(localVelocity.z / 60f, -1f, 1f)); // forward speed

        // 4. Is car upright?
        sensor.AddObservation(transform.up.y); // 1 = upright, -1 = flipped

        // Total: 7 observations
        // Ray Perception Sensor handles the environment scanning
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float throttle = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f); // 0 to 1 only, no reverse
        bool handbrake = actions.ContinuousActions[2] > 0.5f;

        VehicleInput input = new VehicleInput
        {
            steering = steering,
            throttle = throttle,
            isBraking = false,
            isHandbrake = handbrake,
            isBoosting = false
        };
        car.FeedInput(input);

        // --- REWARDS ---

        episodeTimer += Time.fixedDeltaTime;
        stuckTimer += Time.fixedDeltaTime;

        // 1. Reward for moving toward waypoint
        if (waypoints != null && waypoints.Length > 0)
        {
            Vector3 toWaypoint = waypoints[currentWaypointIndex].position - transform.position;
            float distToWaypoint = toWaypoint.magnitude;

            // Small reward for facing and moving toward waypoint
            float dotToWaypoint = Vector3.Dot(transform.forward, toWaypoint.normalized);
            if (dotToWaypoint > 0.7f && car.KMPH > 10f)
            {
                AddReward(0.002f);
            }

            // Reached waypoint
            if (distToWaypoint < waypointReachRadius)
            {
                AddReward(1.0f);
                totalWaypointsReached++;
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                Debug.Log($"Waypoint reached! Total: {totalWaypointsReached}");
            }
        }

        // 2. Punish being flipped
        if (transform.up.y < 0.3f)
        {
            AddReward(-0.5f);
            EndEpisode();
            return;
        }

        // 3. Stuck detection
        if (stuckTimer >= stuckCheckInterval)
        {
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);
            if (distanceMoved < 2f) // barely moved in 5 seconds
            {
                AddReward(-0.5f);
                EndEpisode();
                return;
            }
            lastPosition = transform.position;
            stuckTimer = 0f;
        }

        // 4. Time penalty to encourage efficiency
        AddReward(-0.0005f);

        // 5. Episode timeout
        if (episodeTimer >= maxEpisodeTime)
        {
            EndEpisode();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal");
        ca[1] = Mathf.Max(0f, Input.GetAxis("Vertical"));
        ca[2] = Input.GetKey(KeyCode.Space) ? 1f : 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Wall"))
        {
            AddReward(-1.0f);
            EndEpisode();
        }
    }

    public override void OnEpisodeBegin()
    {
        // Reset car
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Reset tracking
        currentWaypointIndex = 0;
        episodeTimer = 0f;
        stuckTimer = 0f;
        lastPosition = startPosition;
    }
}