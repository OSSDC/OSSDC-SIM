/**
 * Copyright (C) 2016, Jaguar Land Rover
 * This program is licensed under the terms and conditions of the
 * Mozilla Public License, version 2.0.  The full text of the
 * Mozilla Public License is at https://www.mozilla.org/MPL/2.0/
 * 
 * Copyright (c) 2019-2021 LG Electronics, Inc.
 */

using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;


public class VehicleSMI : MonoBehaviour, IVehicleDynamics
{
    private Rigidbody RB;
    public Vector3 Velocity => RB.velocity;
    public Vector3 AngularVelocity => RB.angularVelocity;

    public Transform BaseLink { get { return BaseLinkTransform; } }
    public Transform BaseLinkTransform;

    public float AccellInput { get; set; } = 0f;
    public float BrakeInput { get; set; } = 0f;
    public float SteerInput { get; set; } = 0f;

    public bool HandBrake { get; set; } = false;
    public float CurrentRPM { get; set; } = 0f;
    public float CurrentGear { get; set; } = 1f;
    public bool Reverse { get; set; } = false;
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
    public IgnitionStatus CurrentIgnitionStatus { get; set; } = IgnitionStatus.On;

    public List<AxleInfo> Axles;
    public Vector3 CenterOfMass = new Vector3(0f, 0.35f, 0f);

    [Tooltip("torque at peak of torque curve")]
    public float MaxMotorTorque = 450f;

    [Tooltip("torque at max brake")]
    public float MaxBrakeTorque = 3000f;

    [Tooltip("steering range is +-maxSteeringAngle")]
    public float _MaxSteeringAngle = 39.4f;

    public float MaxSteeringAngle
    {
        get
        {
            return _MaxSteeringAngle;
        }
        set
        {
            _MaxSteeringAngle = value;
        }
    }

    [Tooltip("idle rpm")]
    public float MinRPM = 800f;

    [Tooltip("max rpm")]
    public float MaxRPM = 8299f;

    [Tooltip("gearbox ratios")]
    public float[] GearRatios = new float[] { 4.17f, 3.14f, 2.11f, 1.67f, 1.28f, 1f, 0.84f, 0.67f };
    public float FinalDriveRatio = 2.56f;

    [Tooltip("min time between gear changes")]
    public float ShiftDelay = 0.7f;

    [Tooltip("time interpolated for gear shift")]
    public float ShiftTime = 0.4f;

    [Tooltip("torque curve that gives torque at specific percentage of max RPM")]
    public AnimationCurve RPMCurve;
    [Tooltip("curves controlling whether to shift up at specific rpm, based on throttle position")]
    public AnimationCurve ShiftUpCurve;
    [Tooltip("curves controlling whether to shift down at specific rpm, based on throttle position")]
    public AnimationCurve ShiftDownCurve;

    [Tooltip("Air Drag Coefficient")]
    public float AirDragCoeff = 1.0f;
    [Tooltip("Air Downforce Coefficient")]
    public float AirDownForceCoeff = 2.0f;
    [Tooltip("Tire Drag Coefficient")]
    public float TireDragCoeff = 4.0f;

    [Tooltip("wheel collider damping rate")]
    public float WheelDamping = 1f;

    [Tooltip("autosteer helps the car maintain its heading")]
    [Range(0, 1)]
    public float AutoSteerAmount = 0.338f;

    [Tooltip("traction control limits torque based on wheel slip - traction reduced by amount when slip exceeds the tractionControlSlipLimit")]
    [Range(0, 1)]
    public float TractionControlAmount = 0.675f;
    public float TractionControlSlipLimit = 0.8f;

    [Tooltip("how much to smooth out real RPM")]
    public float RPMSmoothness = 20f;

    private float CurrentTorque = 0f;

    private int NumberOfDrivingWheels;
    private float OldRotation = 0f;
    private float TractionControlAdjustedMaxTorque = 0f;

    private int TargetGear = 1;
    private int LastGear = 1;
    private float GearRatio = 0f;
    private bool Shifting = false;
    private float LastShift = 0.0f;

    private float WheelsRPM = 0f;
    private float MileTicker = 0f;

    private IAgentController Controller;


    private void setVariables()
    {

        var configFile = System.Environment.CurrentDirectory+"/vehicle-dynamics-config.txt";

        // Log.Info("Set vehicle dynamics variables : " + currentDateTime + ", from: "+ configFile);

        Dictionary<string, float> vehichleDynamicsMap = new Dictionary<string, float>();

        var hasValues = false;
        if (File.Exists(@configFile))
        {
            //read sensors placement from file
            string vehichleDynamicsContet = System.IO.File.ReadAllText(@configFile);

            string[] lines = vehichleDynamicsContet.Split('\n');
            foreach (string line in lines)
            {
                if (!line.Contains("//") && line.Trim().Length>0)
                {
                    hasValues=true;
                    var entry = line.Split('=');
                    vehichleDynamicsMap[entry[0].Trim()] = float.Parse((entry[1].Split('f')[0].Trim()));
                }
            }
        }

        if(hasValues)
        {
            RB.mass = vehichleDynamicsMap["RB.mass"]; //600f;
            RB.drag = vehichleDynamicsMap["RB.drag"]; //0.2f;
            RB.angularDrag = vehichleDynamicsMap["RB.angularDrag"]; //0.2f;

            //public Vector3 CenterOfMass = new Vector3(0f, 0.35f, 0f);

            //[Tooltip("torque at peak of torque curve")]
            MaxMotorTorque = vehichleDynamicsMap["MaxMotorTorque"]; //1600f;

            //[Tooltip("torque at max brake")]
            MaxBrakeTorque = vehichleDynamicsMap["MaxBrakeTorque"]; //3000f;

            //[Tooltip("steering range is +-maxSteeringAngle")]
            _MaxSteeringAngle = vehichleDynamicsMap["_MaxSteeringAngle"]; //39.4f;

            //[Tooltip("idle rpm")]
            MinRPM = vehichleDynamicsMap["MinRPM"]; //1600f;

            //[Tooltip("max rpm")]
            MaxRPM = vehichleDynamicsMap["MaxRPM"]; //8000f;

            //[Tooltip("gearbox ratios")]
            GearRatios = new float[] {2.9167f, 1.875f, 1.3809f, 1.1154f, 0.96f, 0.8889f };
            FinalDriveRatio = vehichleDynamicsMap["FinalDriveRatio"]; //3f;

            //[Tooltip("min time between gear changes")]
            ShiftDelay = vehichleDynamicsMap["ShiftDelay"]; //0.01f;

            //[Tooltip("time interpolated for gear shift")]
            ShiftTime = vehichleDynamicsMap["ShiftTime"]; //0.01f;

            //[Tooltip("torque curve that gives torque at specific percentage of max RPM")]
            //public AnimationCurve RPMCurve;
            //[Tooltip("curves controlling whether to shift up at specific rpm, based on throttle position")]
            //public AnimationCurve ShiftUpCurve;
            //[Tooltip("curves controlling whether to shift down at specific rpm, based on throttle position")]
            //public AnimationCurve ShiftDownCurve;

            //[Tooltip("Air Drag Coefficient")]
            AirDragCoeff = vehichleDynamicsMap["AirDragCoeff"]; //0.2f;
            //[Tooltip("Air Downforce Coefficient")]
            AirDownForceCoeff = vehichleDynamicsMap["AirDownForceCoeff"]; //0.2f;
            //[Tooltip("Tire Drag Coefficient")]
            TireDragCoeff = vehichleDynamicsMap["TireDragCoeff"]; //0.2f;

            //[Tooltip("wheel collider damping rate")]
            WheelDamping = vehichleDynamicsMap["WheelDamping"]; //1f;

            //[Tooltip("autosteer helps the car maintain its heading")]
            //[Range(0, 1)]
            AutoSteerAmount = vehichleDynamicsMap["AutoSteerAmount"]; //0.1f;

            //[Tooltip("traction control limits torque based on wheel slip - traction reduced by amount when slip exceeds the tractionControlSlipLimit")]
            //[Range(0, 1)]
            TractionControlAmount = vehichleDynamicsMap["TractionControlAmount"]; //0.675f;
            TractionControlSlipLimit = vehichleDynamicsMap["TractionControlSlipLimit"]; //0.8f;

            //[Tooltip("how much to smooth out real RPM")]
            RPMSmoothness = vehichleDynamicsMap["RPMSmoothness"]; //5f;

        }

    }

    public void Awake()
    {
        RB = GetComponent<Rigidbody>();
        Controller = GetComponent<IAgentController>();

        setVariables();//set custom config

        RB.centerOfMass = CenterOfMass;
        NumberOfDrivingWheels = Axles.Where(a => a.Motor).Count() * 2;
        TractionControlAdjustedMaxTorque = MaxMotorTorque - (TractionControlAmount * MaxMotorTorque);
        foreach (var axle in Axles)
        {
            axle.Left.ConfigureVehicleSubsteps(5f, 30, 10);
            axle.Right.ConfigureVehicleSubsteps(5f, 30, 10);
            axle.Left.wheelDampingRate = WheelDamping;
            axle.Right.wheelDampingRate = WheelDamping;
        }

    }


    // Start is called before the first frame update
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        UpdateWheelVisuals();
    }

    public void FixedUpdate()
    {
        GetInput();
        RB.AddForce(-AirDragCoeff * RB.velocity * RB.velocity.magnitude); //air drag (quadratic)
        RB.AddForce(-AirDownForceCoeff * RB.velocity.sqrMagnitude * transform.up); //downforce (quadratic)
        RB.AddForceAtPosition(-TireDragCoeff * RB.velocity, transform.position); //tire drag (Linear)

        SetGearRatio();
        SetRPM();
        ApplySteer();
        ApplyTorque();
        TractionControl();
        SetMileTick();
    }

    public bool GearboxShiftUp()
    {
        if (Reverse)
        {
            Reverse = false;
        }
        else
        {
            LastGear = Mathf.RoundToInt(CurrentGear);
            TargetGear = LastGear + 1;
            LastShift = Time.time;
            Shifting = true;
        }
        return true;
    }

    public bool GearboxShiftDown()
    {
        if (Mathf.RoundToInt(CurrentGear) == 1)
        {
            Reverse = true;
        }
        else
        {
            LastGear = Mathf.RoundToInt(CurrentGear);
            TargetGear = LastGear - 1;
            LastShift = Time.time;
            Shifting = true;
        }
        return true;
    }

    public bool ShiftFirstGear()
    {
        if (Reverse != false)
        {
            LastGear = 1;
            TargetGear = 1;
            LastShift = Time.time;
            Reverse = false;
        }
        return true;
    }

    public bool ShiftReverse()
    {
        LastGear = 1;
        TargetGear = 1;
        LastShift = Time.time;
        Reverse = true;
        return true;
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
        return true;
    }

    public bool ShiftReverseAutoGearBox()
    {
        if (Time.time - LastShift > ShiftDelay)
        {
            if (CurrentRPM / MaxRPM < ShiftDownCurve.Evaluate(AccellInput) && Mathf.RoundToInt(CurrentGear) > 1)
            {
                GearboxShiftDown();
            }
        }

        if (CurrentGear == 1)
        {
            Reverse = true;
        }
        return true;
    }

    public bool ToggleIgnition()
    {
        CurrentIgnitionStatus = CurrentIgnitionStatus == IgnitionStatus.On ? IgnitionStatus.Off : IgnitionStatus.On;
        return true;
    }

    public bool ToggleHandBrake()
    {
        HandBrake = !HandBrake;
        return true;
    }

    public bool SetHandBrake(bool state)
    {
        HandBrake = state;
        return true;
    }

    public bool ForceReset(Vector3 pos, Quaternion rot)
    {
        RB.MovePosition(pos);
        RB.MoveRotation(rot);
        RB.velocity = Vector3.zero;
        RB.angularVelocity = Vector3.zero;
        CurrentGear = 1;
        CurrentRPM = 0f;
        AccellInput = 0f;
        SteerInput = 0f;

        foreach (var axle in Axles)
        {
            axle.Left.brakeTorque = Mathf.Infinity;
            axle.Right.brakeTorque = Mathf.Infinity;
            axle.Left.motorTorque = 0f;
            axle.Right.motorTorque = 0f;
        }
        return true;
    }

    private void UpdateWheelVisuals()
    {
        foreach (var axle in Axles)
        {
            ApplyLocalPositionToVisuals(axle.Left, axle.LeftVisual);
            ApplyLocalPositionToVisuals(axle.Right, axle.RightVisual);
        }
    }

    private void ApplyLocalPositionToVisuals(WheelCollider collider, GameObject visual)
    {
        if (visual == null || collider == null)
        {
            return;
        }

        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        visual.transform.position = position;
        visual.transform.rotation = rotation;
    }

    private void SetMileTick()
    {
        float deltaDistance = WheelsRPM / 60.0f * (Axles[1].Left.radius * 2.0f * Mathf.PI) * Time.fixedDeltaTime;
        MileTicker += deltaDistance;
        if ((MileTicker * 0.00062137f) > 1)
        {
            MileTicker = 0;
        }
    }

    private void SetGearRatio()
    {
        GearRatio = Mathf.Lerp(GearRatios[Mathf.FloorToInt(CurrentGear) - 1], GearRatios[Mathf.CeilToInt(CurrentGear) - 1], CurrentGear - Mathf.Floor(CurrentGear));
        if (Reverse)
        {
            GearRatio = -1.0f * GearRatios[0];
        }

        AutoGearBox();
    }

    private void AutoGearBox()
    {
        if (CurrentIgnitionStatus != IgnitionStatus.On)
        {
            return;
        }

        //check delay so we cant shift up/down too quick
        //FIXME lock gearbox for certain amount of time if user did override
        if (Time.time - LastShift > ShiftDelay)
        {
            //shift up
            if (CurrentRPM / MaxRPM > ShiftUpCurve.Evaluate(AccellInput) && Mathf.RoundToInt(CurrentGear) < GearRatios.Length)
            {
                //don't shift up if we are just spinning in 1st
                if (Mathf.RoundToInt(CurrentGear) > 1 || RB.velocity.magnitude > 15f)
                {
                    GearboxShiftUp();
                }
            }
            //else down
            if (CurrentRPM / MaxRPM < ShiftDownCurve.Evaluate(AccellInput) && Mathf.RoundToInt(CurrentGear) > 1)
            {
                GearboxShiftDown();
            }

        }

        if (Shifting)
        {
            float lerpVal = (Time.time - LastShift) / ShiftTime;
            CurrentGear = Mathf.Lerp(LastGear, TargetGear, lerpVal);
            if (lerpVal >= 1f)
                Shifting = false;
        }

        //clamp to gear range
        if (CurrentGear >= GearRatios.Length)
        {
            CurrentGear = GearRatios.Length - 1;
        }
        else if (CurrentGear < 1)
        {
            CurrentGear = 1;
        }
    }

    private void SetRPM()
    {
        //calc engine RPM from wheel rpm
        WheelsRPM = (Axles[1].Right.rpm + Axles[1].Left.rpm) / 2f;
        if (WheelsRPM < 0)
        {
            WheelsRPM = 0;
        }

        // if the engine is on, the fuel injectors are going to be triggered at minRPM
        // to keep the engine running.  If the engine is OFF, then the engine will eventually
        // go all the way down to 0, because there's nothing keeping it spinning.
        var minPossibleRPM = CurrentIgnitionStatus == IgnitionStatus.On ? MinRPM : 0.0f;
        CurrentRPM = Mathf.Lerp(CurrentRPM, minPossibleRPM + (WheelsRPM * FinalDriveRatio * GearRatio), Time.fixedDeltaTime * RPMSmoothness);
        if (CurrentRPM < 0.02f)
        {
            CurrentRPM = 0.0f;
        }
    }

    private void ApplySteer()
    {
        //convert inputs to torques
        float steer = MaxSteeringAngle * SteerInput;
        foreach (var axle in Axles)
        {
            if (axle.Steering)
            {
                axle.Left.steerAngle = steer;
                axle.Right.steerAngle = steer;
            }
        }

        AutoSteer();
    }

    private void AutoSteer()
    {
        if (CurrentIgnitionStatus != IgnitionStatus.On)
        {
            return;
        }

        foreach (var axle in Axles) //find out which wheels are on the ground
            {
            axle.GroundedLeft = axle.Left.GetGroundHit(out axle.HitLeft);
            axle.GroundedRight = axle.Right.GetGroundHit(out axle.HitRight);

            if (axle.GroundedLeft == false || axle.GroundedRight == false)
            {
                return; //bail if a wheel isn't on the ground
            }
        }

        var yawRate = OldRotation - transform.eulerAngles.y;
        if (Mathf.Abs(yawRate) < 10f) //don't adjust if the yaw rate is super high
        {
            RB.velocity = Quaternion.AngleAxis(yawRate * AutoSteerAmount, Vector3.up) * RB.velocity;
        }

        OldRotation = transform.eulerAngles.y;
    }

    private void ApplyTorque()
    {
        CurrentTorque = (float.IsNaN(CurrentRPM / MaxRPM)) ? 0.0f : RPMCurve.Evaluate(CurrentRPM / MaxRPM) * GearRatio * FinalDriveRatio * TractionControlAdjustedMaxTorque;

        // acceleration is ignored when engine is not running, brakes are available still.
        if (AccellInput >= 0)
        {
            //motor
            float torquePerWheel = CurrentIgnitionStatus == IgnitionStatus.On ? AccellInput * (CurrentTorque / NumberOfDrivingWheels) : 0f;
            //Debug.Log(torquePerWheel);
            foreach (var axle in Axles)
            {
                if (axle.Motor)
                {
                    axle.Left.motorTorque = torquePerWheel;
                    axle.Right.motorTorque = torquePerWheel;
                }

                axle.Left.brakeTorque = 0f;
                axle.Right.brakeTorque = 0f;
            }

        }
        // TODO: to get brake + accelerator working at the same time, modify this area.
        // You'll need to do some work to separate the brake and accel pedal inputs, though.
        // TODO: handBrake should apply full braking to rear axle (possibly all axles), without
        // changing the accelInput
        else
        {
            //brakes
            foreach (var axle in Axles)
            {
                var brakeTorque = MaxBrakeTorque * AccellInput * -1 * axle.BrakeBias;
                axle.Left.brakeTorque = brakeTorque;
                axle.Right.brakeTorque = brakeTorque;
                axle.Left.motorTorque = 0f;
                axle.Right.motorTorque = 0f;
            }
        }
    }

    private void TractionControl()
    {
        foreach (var axle in Axles)
        {
            if (axle.Motor)
            {
                if (axle.Left.isGrounded)
                    AdjustTractionControlTorque(axle.HitLeft.forwardSlip);

                if (axle.Right.isGrounded)
                    AdjustTractionControlTorque(axle.HitRight.forwardSlip);
            }
        }
    }

    private void AdjustTractionControlTorque(float forwardSlip)
    {
        if (forwardSlip >= TractionControlSlipLimit && TractionControlAdjustedMaxTorque >= 0)
        {
            TractionControlAdjustedMaxTorque -= 10 * TractionControlAmount;
            if (TractionControlAdjustedMaxTorque < 0)
                TractionControlAdjustedMaxTorque = 0f;
        }
        else
        {
            TractionControlAdjustedMaxTorque += 10 * TractionControlAmount;
            if (TractionControlAdjustedMaxTorque > MaxMotorTorque)
                TractionControlAdjustedMaxTorque = MaxMotorTorque;
        }
    }

    private void GetInput()
    {
        if (Controller != null)
        {
            SteerInput = Controller.SteerInput;
            AccellInput = Controller.AccelInput - Controller.BrakeInput;
        }

        if (HandBrake)
        {
            AccellInput = -1.0f; // TODO better way using Accel and Brake
        }
    }
}
