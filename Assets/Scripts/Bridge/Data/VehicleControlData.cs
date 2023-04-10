/**
 * Copyright (c) 2019-2021 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */

using UnityEngine;

namespace Simulator.Bridge.Data
{
    public enum GearPosition
    {
        Neutral,
        Drive,
        Reverse,
        Parking,
        Low,
        Middle,
        High
    };

    public class VehicleControlData
    {
        // common
        public float? Acceleration; // 0..1
        public float? Braking; // 0..1

        // autoware
        public float? Velocity;
        public float? SteerAngularVelocity;
        public float? SteerAngle;
        public GearPosition? TargetGear;
        public bool ShiftGearUp;
        public bool ShiftGearDown;

        // apollo 
        public float? SteerRate;
        public float? SteerTarget;
        public double? TimeStampSec;
        public GearPosition? CurrentGear;

        // lgsvl
        public float? SteerInput;
    }
}
