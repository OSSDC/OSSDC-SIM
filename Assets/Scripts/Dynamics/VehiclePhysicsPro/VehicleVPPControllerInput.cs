/**
 * Copyright (c) 2021 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using VehiclePhysics;

public class VehicleVPPControllerInput : MonoBehaviour
{
    public VehicleBase Vehicle;

    public void SetSteer(float steerNormalized)
    {
        const float MAX_STEER = 14.4f * Mathf.Deg2Rad;
        int steerVpp = (int)(Mathf.Clamp(steerNormalized / MAX_STEER, -1.0f, 1.0f) * 10000);
        Vehicle.data.Set(Channel.Input, InputData.Steer, steerVpp);
    }

    public void SetHandBrake(bool handBrakeIsActive)
    {
        int handBrakeLevel = 0;
        if (handBrakeIsActive)
        {
            handBrakeLevel = 10000;
        }

        Vehicle.data.Set(Channel.Input, InputData.Handbrake, handBrakeLevel);
    }

    public void SetAccel(float accelNormalized)
    {
        int throttleVpp = (int)(accelNormalized * 10000);
        Vehicle.data.Set(Channel.Input, InputData.Throttle, throttleVpp);
    }

    public void SetBrake(float brakeNormalized)
    {
        int brakeVpp = (int)(brakeNormalized * 10000);
        Vehicle.data.Set(Channel.Input, InputData.Brake, brakeVpp);
    }

    public int GetWheelIndex(int axle, VehicleBase.WheelPos pos)
    {
        return Vehicle.GetWheelIndex(axle, pos);
    }

    public float GetWheelAngularVelocity(int wheelIdx)
    {
        var wheel = Vehicle.wheelState[wheelIdx];
        if (wheel == null)
        {
            Debug.LogWarning($"Wheel at index {wheelIdx} is null");
            return -1;
        }

        var rpm = wheel.angularVelocity * Block.WToRpm;
        var rps = rpm / 60.0;
        var rads = rps * Math.PI * 2.0;
        return (float)rads;
    }

    public void GearShiftUpAuto()
    {
        // Increase the gear by 1
        Vehicle.data.Set(Channel.Input, InputData.GearShift, 1);
    }

    public void GearShiftDownAuto()
    {
        // Decrease the gear by 1
        Vehicle.data.Set(Channel.Input, InputData.GearShift, -1);
    }

    public void SetIgnition(int state)
    {
        Vehicle.data.Set(Channel.Input, InputData.Key, state);
    }

    public void SwitchToFirstGearFromReverse()
    {
        // NOTE(dvd): Unneeded - no reverse.
    }

    public void SwitchToReverse()
    {
        // NOTE(dvd): Unneeded - no reverse.
    }

    public int GetGear()
    {
        return Vehicle.data.Get(Channel.Vehicle, VehicleData.GearboxGear);
    }

    public int GetRPMEngine()
    {
        return (int)(Vehicle.data.Get(Channel.Vehicle, VehicleData.EngineRpm) / 1000.0f);
    }
}
