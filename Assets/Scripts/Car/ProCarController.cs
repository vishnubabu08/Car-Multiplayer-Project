using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ProCarController : MonoBehaviour
{
    public enum DriveType { FWD, RWD, AWD }
    public enum GearboxType { Automatic, Manual }

    [Header("Drivetrain Settings")]
    [SerializeField] private DriveType driveType = DriveType.AWD;
    [SerializeField] private GearboxType gearbox = GearboxType.Automatic;
    public AnimationCurve enginePowerCurve;
    public float[] gearRatios = { 3.2f, 2.1f, 1.5f, 1.1f, 0.8f, 0.6f };

    [Header("Performance Specs")]
    public float maxRPM = 8000f;
    public float minRPM = 1000f;
    public float brakePower = 3000f;
    public float downforceMultiplier = 50f;
    public float maxSteerAngle = 35f;

    [Header("Drifting & Traction")]
    public float handbrakeFrictionMultiplier = 2f;
    [SerializeField] private float driftSmoothFactor = 5f;

    [Header("Physics Setup")]
    [SerializeField] private Transform centerOfMass;
    [SerializeField] private WheelCollider[] wheels = new WheelCollider[4]; // 0:FL, 1:FR, 2:RL, 3:RR
    [SerializeField] private Transform[] wheelMeshes = new Transform[4];

    // --- Modern C# Events for UI, Audio, and VFX ---
    public event Action<float, float, int, bool> OnTelemetryUpdated;
    public event Action<bool> OnDriftStateChanged;

    // Public getters for other scripts
    public float KMPH { get; private set; }
    public float EngineRPM { get; private set; }
    public int CurrentGear { get; private set; }
    public bool IsReversing { get; private set; }

    private Rigidbody rb;
    private VehicleInput currentInput;

    // Dimensions for Steering
    private float wheelbase = 2.55f;
    private float trackWidth = 1.5f;

    // Internal Physics Variables
    private float lastShiftTime;
    private float driftFactor;
    private WheelFrictionCurve forwardFriction, sidewayFriction;

    [Header("Stability")]
    public float antiRollForce = 5000f; // Keeps the car from flipping

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (centerOfMass != null) rb.centerOfMass = centerOfMass.localPosition;

        // Ensure we capture exact dimensions for perfect Ackermann steering
        if (wheels[0] != null && wheels[2] != null && wheels[1] != null)
        {
            wheelbase = Vector3.Distance(wheels[0].transform.position, wheels[2].transform.position);
            trackWidth = Vector3.Distance(wheels[0].transform.position, wheels[1].transform.position);
        }
    }

    public void FeedInput(VehicleInput input)
    {
        currentInput = input;
    }

    private void FixedUpdate()
    {
        CalculateTelemetry();
        ApplySteering();
        ApplyMotorAndBrakes();
        ApplyDownforce();
        HandleGears();
        UpdateWheelMeshes();
        HandleTractionAndDrift();

        ApplyAntiRollBars(); // <--- ADD THIS HERE!

        OnTelemetryUpdated?.Invoke(KMPH, EngineRPM, CurrentGear, IsReversing);
    }

    private void CalculateTelemetry()
    {
        KMPH = rb.linearVelocity.magnitude * 3.6f;

        float wheelRPM = (wheels[2].rpm + wheels[3].rpm) / 2f;

        // Accurate Reversing check (pressing backward while wheels spin backward)
        IsReversing = wheelRPM < -10f && currentInput.throttle < 0;

        float targetRPM = 1000f + (Mathf.Abs(wheelRPM) * 3.6f * gearRatios[CurrentGear]);
        EngineRPM = Mathf.Lerp(EngineRPM, Mathf.Clamp(targetRPM, minRPM, maxRPM), Time.fixedDeltaTime * 5f);
    }

    private void ApplyMotorAndBrakes()
    {
        // Find out if the car is physically moving forward
        float forwardVelocity = transform.InverseTransformDirection(rb.linearVelocity).z;
        bool isMovingForward = forwardVelocity > 1f;

        float currentTorque = enginePowerCurve.Evaluate(EngineRPM) * gearRatios[CurrentGear];

        for (int i = 0; i < 4; i++)
        {
            // Handbrake overrides all (locks rear wheels)
            if (currentInput.isHandbrake && i >= 2)
            {
                wheels[i].brakeTorque = brakePower;
                wheels[i].motorTorque = 0f;
            }
            // "Smart Brake": If pressing back while moving forward -> Apply Brakes
            else if (currentInput.throttle < 0 && isMovingForward)
            {
                wheels[i].brakeTorque = brakePower;
                wheels[i].motorTorque = 0f;
            }
            // Standard button brake
            else if (currentInput.isBraking)
            {
                wheels[i].brakeTorque = brakePower;
                wheels[i].motorTorque = 0f;
            }
            // Otherwise -> Apply Gas or Reverse
            else
            {
                wheels[i].brakeTorque = 0f;

                if (driveType == DriveType.AWD ||
                   (driveType == DriveType.FWD && i < 2) ||
                   (driveType == DriveType.RWD && i >= 2))
                {
                    wheels[i].motorTorque = currentTorque * currentInput.throttle;
                }
            }
        }
    }

    private void ApplySteering()
    {
        // Allow 70% of max steering at top speed instead of locking it up entirely
        float speedFactor = Mathf.Clamp01(KMPH / 200f);
        float currentMaxSteer = Mathf.Lerp(maxSteerAngle, maxSteerAngle * 0.7f, speedFactor);

        // Cap the dynamic radius so it doesn't get impossibly wide at 150km/h+
        float dynamicRadius = 6f + Mathf.Clamp(KMPH / 25f, 0f, 6f);

        float innerAngle = 0f, outerAngle = 0f;

        if (currentInput.steering > 0)
        {
            innerAngle = Mathf.Rad2Deg * Mathf.Atan(wheelbase / (dynamicRadius - (trackWidth / 2f))) * currentInput.steering;
            outerAngle = Mathf.Rad2Deg * Mathf.Atan(wheelbase / (dynamicRadius + (trackWidth / 2f))) * currentInput.steering;
        }
        else if (currentInput.steering < 0)
        {
            innerAngle = Mathf.Rad2Deg * Mathf.Atan(wheelbase / (dynamicRadius + (trackWidth / 2f))) * currentInput.steering;
            outerAngle = Mathf.Rad2Deg * Mathf.Atan(wheelbase / (dynamicRadius - (trackWidth / 2f))) * currentInput.steering;
        }

        wheels[0].steerAngle = Mathf.Clamp(innerAngle, -currentMaxSteer, currentMaxSteer);
        wheels[1].steerAngle = Mathf.Clamp(outerAngle, -currentMaxSteer, currentMaxSteer);
    }

    private void HandleTractionAndDrift()
    {
        float driftSmooth = 0.7f * Time.fixedDeltaTime;
        bool isDrifting = false;

        if (currentInput.isHandbrake)
        {
            forwardFriction = wheels[2].forwardFriction;
            sidewayFriction = wheels[2].sidewaysFriction;

            float velocity = 0;

            // 1. Drop friction for the REAR wheels so the tail slides out
            sidewayFriction.extremumValue = sidewayFriction.asymptoteValue = forwardFriction.asymptoteValue =
                Mathf.SmoothDamp(forwardFriction.asymptoteValue, driftFactor * handbrakeFrictionMultiplier, ref velocity, driftSmooth);

            for (int i = 2; i < 4; i++) // REAR WHEELS ONLY
            {
                wheels[i].sidewaysFriction = sidewayFriction;
                wheels[i].forwardFriction = forwardFriction;
            }

            // 2. Keep FRONT wheels grippy (1.1f) so you can steer the drift!
            forwardFriction.extremumValue = forwardFriction.asymptoteValue = 1.1f;
            sidewayFriction.extremumValue = sidewayFriction.asymptoteValue = 1.1f;

            for (int i = 0; i < 2; i++) // FRONT WHEELS ONLY
            {
                wheels[i].sidewaysFriction = sidewayFriction;
                wheels[i].forwardFriction = forwardFriction;
            }

            // Push the car through the drift
            rb.AddForce(transform.forward * (KMPH / 400f) * 1000f);
        }
        else
        {
            forwardFriction = wheels[0].forwardFriction;
            sidewayFriction = wheels[0].sidewaysFriction;

            // Restore normal grip based on speed
            forwardFriction.extremumValue = forwardFriction.asymptoteValue = sidewayFriction.extremumValue = sidewayFriction.asymptoteValue =
                ((KMPH * handbrakeFrictionMultiplier) / 300f) + 1f;

            for (int i = 0; i < 4; i++)
            {
                wheels[i].forwardFriction = forwardFriction;
                wheels[i].sidewaysFriction = sidewayFriction;
            }
        }

        // Slip detection for VFX and driftFactor adjustments
        for (int i = 2; i < 4; i++)
        {
            wheels[i].GetGroundHit(out WheelHit hit);

            if (Mathf.Abs(hit.sidewaysSlip) >= 0.3f || Mathf.Abs(hit.forwardSlip) >= 0.3f)
                isDrifting = true;

            if (hit.sidewaysSlip < 0) driftFactor = (1 + -currentInput.steering) * Mathf.Abs(hit.sidewaysSlip);
            if (hit.sidewaysSlip > 0) driftFactor = (1 + currentInput.steering) * Mathf.Abs(hit.sidewaysSlip);
        }

        OnDriftStateChanged?.Invoke(isDrifting);
    }

    private void HandleGears()
    {
        if (Time.time - lastShiftTime < 0.5f) return;

        if (gearbox == GearboxType.Automatic)
        {
            if (EngineRPM > maxRPM - 500f && CurrentGear < gearRatios.Length - 1 && !IsReversing)
            {
                CurrentGear++;
                lastShiftTime = Time.time;
            }
            else if (EngineRPM < minRPM + 500f && CurrentGear > 0)
            {
                CurrentGear--;
                lastShiftTime = Time.time;
            }
        }
    }



    private void ApplyDownforce()
    {
        rb.AddForce(-transform.up * downforceMultiplier * rb.linearVelocity.magnitude);
    }

    private void UpdateWheelMeshes()
    {
        for (int i = 0; i < 4; i++)
        {
            if (wheelMeshes[i] == null) continue;
            wheels[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
            wheelMeshes[i].position = pos;
            wheelMeshes[i].rotation = rot;
        }
    }

    private void ApplyAntiRollBars()
    {
        // Front Axle
        ApplyAxleAntiRoll(wheels[0], wheels[1]);
        // Rear Axle
        ApplyAxleAntiRoll(wheels[2], wheels[3]);
    }

    private void ApplyAxleAntiRoll(WheelCollider wheelL, WheelCollider wheelR)
    {
        WheelHit hit;
        float travelL = 1.0f;
        float travelR = 1.0f;

        bool groundedL = wheelL.GetGroundHit(out hit);
        if (groundedL) travelL = (-wheelL.transform.InverseTransformPoint(hit.point).y - wheelL.radius) / wheelL.suspensionDistance;

        bool groundedR = wheelR.GetGroundHit(out hit);
        if (groundedR) travelR = (-wheelR.transform.InverseTransformPoint(hit.point).y - wheelR.radius) / wheelR.suspensionDistance;

        float antiRollForceDifference = (travelL - travelR) * antiRollForce;

        if (groundedL) rb.AddForceAtPosition(wheelL.transform.up * -antiRollForceDifference, wheelL.transform.position);
        if (groundedR) rb.AddForceAtPosition(wheelR.transform.up * antiRollForceDifference, wheelR.transform.position);
    }
}