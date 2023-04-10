using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VehiclePhysics;

[System.Serializable]
public class VPAxleInfo
{
    public VPWheelCollider Left;
    public VPWheelCollider Right;
    public GameObject LeftVisual;
    public GameObject RightVisual;
    public bool Motor;
    public bool Steering;
    public float BrakeBias = 0.5f;

    [System.NonSerialized]
    public WheelHit HitLeft;
    [System.NonSerialized]
    public WheelHit HitRight;
    [System.NonSerialized]
    public bool GroundedLeft = false;
    [System.NonSerialized]
    public bool GroundedRight = false;
}
