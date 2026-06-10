using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(ProCarController))]
public class AITrainingDriver : Agent
{
    private ProCarController car;
    private Vector3 startPosition;
    private Quaternion startRotation;

    public override void Initialize()
    {
        car = GetComponent<ProCarController>();
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(Mathf.Clamp01(car.KMPH / 200f));
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        VehicleInput input = new VehicleInput
        {
            steering = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f),
            throttle = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f),
            isBraking = actions.ContinuousActions[2] > 0.5f,
            isHandbrake = actions.ContinuousActions[3] > 0.5f,
            isBoosting = false
        };

        car.FeedInput(input);

        if (car.KMPH > 30f)
        {
            AddReward(0.001f);
        }
    }

    // THIS IS THE NEW FIX: It stops the yellow Heuristic warnings!
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = 0f; // No steering
        continuousActionsOut[1] = 0f; // No throttle
        continuousActionsOut[2] = 0f; // No brake
        continuousActionsOut[3] = 0f; // No handbrake
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(-1.0f);
            EndEpisode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Waypoint"))
        {
            AddReward(1.0f);
        }
    }

    public override void OnEpisodeBegin()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}