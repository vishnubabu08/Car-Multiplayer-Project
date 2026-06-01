using UnityEngine;

[RequireComponent(typeof(ProCarController))]
public class LocalPlayerInput : MonoBehaviour
{
    private ProCarController car;

    private void Awake() { car = GetComponent<ProCarController>(); }

    private void Update()
    {
        VehicleInput input = new VehicleInput
        {
            steering = Input.GetAxis("Horizontal"),
            throttle = Input.GetAxis("Vertical"),
            isBraking = false, // <-- THIS MUST BE FALSE!
            isHandbrake = Input.GetKey(KeyCode.Space),
            isBoosting = Input.GetKey(KeyCode.LeftShift)
        };
        car.FeedInput(input);
    }
}