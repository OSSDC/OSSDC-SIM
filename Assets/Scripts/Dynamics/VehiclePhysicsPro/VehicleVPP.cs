/**
 * Copyright (c) 2021 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VehiclePhysics;

public class VehicleVPP : MonoBehaviour, IVehicleDynamics
{
    public Text uiText;

    public Rigidbody RB { get; private set; }
    private Vector3 lastRBPosition;

    public Vector3 Velocity => RB.velocity;
    public Vector3 AngularVelocity => RB.angularVelocity;

    public Transform BaseLink
    {
        get { return BaseLinkTransform; }
    }

    public Transform BaseLinkTransform;

    public float AccellInput { get; set; } = 0f;
    public float BrakeInput { get; set; } = 0f;
    public float SteerInput { get; set; } = 0f;

    public int TargetGear { get; set; } = 0;
    public int ManualGear { get; set; } = 0;

    public bool HandBrake { get; set; } = false;
    public float CurrentRPM { get; set; } = 0f;

    public float CurrentGear { get; set; } = 1f;
    public bool Reverse { get; set; } = false;

    public bool PurpleFlag { get; set;} = false;

    public List<VPAxleInfo> Axles = new List<VPAxleInfo>();

    public float WheelAngle
    {
        get
        {
            if (Axles != null && Axles.Count > 0 && Axles[0] != null)
            {
                return (Axles[0].Left.steerAngle + Axles[0].Right.steerAngle) * 0.5f;
            }

            return 0.0f;
        }
    }

    public float Speed
    {
        get
        {
            if (RB != null)
            {
                return RB.velocity.magnitude;
            }

            return 0f;
        }
    }

    public float MeasuredSpeed { get; set; }

    public float MaxSteeringAngle { get; set; }

    public IgnitionStatus CurrentIgnitionStatus { get; set; }

    private VehicleController Controller;
    private VehicleVPPControllerInput VehicleVPPControllerInput;

    private void Awake()
    {
        RB = GetComponent<Rigidbody>();
        Controller = GetComponent<VehicleController>();

        VehicleVPPControllerInput = GetComponent<VehicleVPPControllerInput>();
        VehicleVPPControllerInput.Vehicle = GetComponent<VehicleBase>();
        if (VehicleVPPControllerInput.Vehicle == null)
        {
            Debug.LogWarning("Could not find VehicleBase.");
        }

        UpdateGearFromVpp();
        StartEngine();
    }

    void OnEnable()
    {
        lastRBPosition = RB.position;
        StartEngine();
    }

    public void FixedUpdate()
    {
        if (VehicleVPPControllerInput.GetRPMEngine() == 0) {
            StartEngine();
        }
        if (Controller != null)
        {
            SteerInput = Controller.SteerInput;
            AccellInput = Controller.AccelInput;
            BrakeInput = Controller.BrakeInput;
            TargetGear = Controller.TargetGear;
        }

        VehicleVPPControllerInput.SetHandBrake(HandBrake);

        VehicleVPPControllerInput.SetSteer(SteerInput);
        VehicleVPPControllerInput.SetAccel(AccellInput);
        VehicleVPPControllerInput.SetBrake(BrakeInput);

        if (TargetGear != ManualGear)
        {
            if (TargetGear == ManualGear + 1) {
                ((IVehicleDynamics)this).GearboxShiftUp();
                ManualGear = TargetGear;
            } else if (TargetGear == ManualGear - 1) {
                ((IVehicleDynamics)this).GearboxShiftDown();
                ManualGear = TargetGear;
            }
        }

        MeasuredSpeed = ((RB.position - lastRBPosition) / Time.fixedDeltaTime).magnitude;
        lastRBPosition = RB.position;

        UpdateGearFromVpp();
        CurrentRPM = VehicleVPPControllerInput.GetRPMEngine();
    }

    bool IVehicleDynamics.GearboxShiftUp()
    {
        VehicleVPPControllerInput.GearShiftUpAuto();
        UpdateGearFromVpp();
        return false;
    }

    bool IVehicleDynamics.GearboxShiftDown()
    {
        VehicleVPPControllerInput.GearShiftDownAuto();
        UpdateGearFromVpp();
        return false;
    }

    public bool ShiftFirstGear()
    {
        VehicleVPPControllerInput.SwitchToFirstGearFromReverse();
        UpdateGearFromVpp();
        return false;
    }

    public bool ShiftReverse()
    {
        VehicleVPPControllerInput.SwitchToReverse();
        UpdateGearFromVpp();
        return false;
    }

    public bool ToggleReverse()
    {
        if (Reverse)
        {
            ShiftFirstGear();
        }
        else
        {
            ShiftReverse();
        }

        return false;
    }

    public bool ShiftReverseAutoGearBox()
    {
        VehicleVPPControllerInput.GearShiftDownAuto();
        UpdateGearFromVpp();
        return false;
    }

    public bool ToggleIgnition()
    {
        switch (CurrentIgnitionStatus)
        {
            case IgnitionStatus.Off:
                StartEngine();
                break;
            case IgnitionStatus.On:
                StopEngine();
                break;
        }

        return false;
    }

    public bool ToggleHandBrake()
    {
        HandBrake = !HandBrake;
        return false;
    }

    public bool SetHandBrake(bool handBrakeIsActive)
    {
        HandBrake = handBrakeIsActive;
        return false;
    }

    public bool ForceReset(Vector3 pos, Quaternion rot)
    {
        RB.MovePosition(pos);
        RB.MoveRotation(rot);
        RB.velocity = Vector3.zero;
        RB.angularVelocity = Vector3.zero;

        var vpVehicleController = GetComponent<VPVehicleController>();
        vpVehicleController.enabled = false;
        vpVehicleController.enabled = true;

        CurrentRPM = VehicleVPPControllerInput.GetRPMEngine();
        AccellInput = 0f;
        BrakeInput = 0f;
        SteerInput = 0f;
        HandBrake = false;

        TargetGear = 0;
        ManualGear = 0;
        CurrentGear = 0;

        VehicleVPPControllerInput.SetSteer(SteerInput);
        VehicleVPPControllerInput.SetHandBrake(HandBrake);
        VehicleVPPControllerInput.SetAccel(AccellInput);
        VehicleVPPControllerInput.SetBrake(BrakeInput);
        return false;
    }

    public bool GearboxShiftUp()
    {
        VehicleVPPControllerInput.GearShiftUpAuto();
        UpdateGearFromVpp();
        return false;
    }

    private void UpdateGearFromVpp()
    {
        int gearNew = VehicleVPPControllerInput.GetGear();
        Reverse = gearNew == -1;
        CurrentGear = Math.Abs(gearNew);
    }

    public void StartEngine()
    {
        VehicleVPPControllerInput.SetIgnition(1);
        CurrentIgnitionStatus = IgnitionStatus.On;
    }

    public void StopEngine()
    {
        VehicleVPPControllerInput.SetIgnition(-1);
        CurrentIgnitionStatus = IgnitionStatus.Off;
    }
}