using UnityEngine;

// This struct is easily sent over the network for multiplayer syncing!
public struct VehicleInput
{
    public float steering;   // -1.0 to 1.0
    public float throttle;   // -1.0 (Reverse) to 1.0 (Forward)
    public bool isBraking;
    public bool isHandbrake;
    public bool isBoosting;
    public bool shiftUp;     // For manual gears
    public bool shiftDown;
}