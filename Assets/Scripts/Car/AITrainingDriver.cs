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
    public float maxEpisodeTime = 300f; // Max time before a forced reset (set to 99999 if just playing)
    public float minSpeedThreshold = 2f;
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
    private float lastSteering = 0f;

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
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // --- 1. THE NEW 4-PEDAL SETUP ---
        float steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float throttle = Mathf.Clamp(actions.ContinuousActions[1], 0f, 1f);
        float brakeReverse = Mathf.Clamp(actions.ContinuousActions[2], 0f, 1f);
        bool handbrake = actions.ContinuousActions[3] > 0.5f;

        // THE OVERRIDE: Prevent the AI from pressing Gas and Brake at the same time!
        if (throttle > 0.1f)
        {
            brakeReverse = 0f; // If gas is pressed, force the brake to 0.
        }

        VehicleInput input = new VehicleInput
        {
            steering = steering,
            throttle = throttle,
            isBraking = brakeReverse > 0.1f,
            isHandbrake = handbrake,
            isBoosting = false
        };
        car.FeedInput(input);

        // ... (Keep everything else below this exactly the same)
        // --- 2. TIMERS ---
        episodeTimer += Time.fixedDeltaTime;
        stuckTimer += Time.fixedDeltaTime;

        // --- 3. WAYPOINT REWARDS ---
        if (waypoints != null && waypoints.Length > 0)
        {
            Vector3 toWaypoint = waypoints[currentWaypointIndex].position - transform.position;
            float distToWaypoint = toWaypoint.magnitude;

            // FIX: Removed the "> 10f KMH" requirement. 
            // Now it gets a tiny reward simply for looking at the waypoint, even if stopped.
            if (Vector3.Dot(transform.forward, toWaypoint.normalized) > 0.7f)
                AddReward(0.001f);

            if (distToWaypoint < waypointReachRadius)
            {
                AddReward(1.0f);
                totalWaypointsReached++;
                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            }
        }

        // --- 4. SPEED ENCOURAGEMENT ---
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        // FIX: Lowered the threshold from 5f to 1f, and slightly increased the reward.
        // Now it gets rewarded the second it figures out how to creep forward!
        if (localVelocity.z > 1f)
        {
            AddReward(localVelocity.z * 0.0005f);
        }

        // --- 5. THE SOFT STEERING TAX (Cures the "Sniffing Dog") ---
        float steeringJerk = Mathf.Abs(steering - lastSteering);
        AddReward(-steeringJerk * 0.0005f); // Very light penalty to stop rapid wiggling
        lastSteering = steering;

        // --- 6. STUCK PENALTY (TRAINING MODE) ---
        if (stuckTimer >= stuckCheckInterval)
        {
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);
            if (distanceMoved < minSpeedThreshold)
            {
                AddReward(-1.0f);
                EndEpisode(); // <--- PUT THIS BACK IN FOR TRAINING!
                return;       // <--- ADD THIS SO THE CODE STOPS HERE
            }
            lastPosition = transform.position;
            stuckTimer = 0f;
        }

        // Failsafe timeout only
        if (episodeTimer >= maxEpisodeTime) EndEpisode();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Updated to support manual testing with the 4-pedal system
        var ca = actionsOut.ContinuousActions;
        ca[0] = Input.GetAxis("Horizontal"); // A/D for steering
        ca[1] = Mathf.Max(0f, Input.GetAxis("Vertical")); // W/Up Arrow for Gas
        ca[2] = Mathf.Abs(Mathf.Min(0f, Input.GetAxis("Vertical"))); // S/Down Arrow for Brake/Reverse
        ca[3] = Input.GetKey(KeyCode.Space) ? 1f : 0f; // Spacebar for Handbrake
    }

    // --- 7. COLLISION HANDLING (NO TELEPORTING) ---
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Just hurts the score. Car will bounce and keep going!
            AddReward(-2.0f);
        }

        if (collision.gameObject.CompareTag("Player"))
        {
            // Heavy penalty for hitting the player to teach avoidance.
            AddReward(-5.0f);
        }
    }

    public override void OnEpisodeBegin()
    {
        // Reset car physics
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Reset tracking variables
        currentWaypointIndex = 0;
        totalWaypointsReached = 0;
        episodeTimer = 0f;
        stuckTimer = 0f;
        lastPosition = startPosition;
        lastSteering = 0f;
    }
}