﻿using PiTop.Abstractions;

using System;
using UnitsNet;

namespace PiTop.MakerArchitecture.Expansion
{
    public class EncoderMotor : IConnectedDevice
    {
        private const int MMK_STANDARD_GEAR_RATIO = 42;
        private const int MAX_DC_MOTOR_RPM = 6000;
        private readonly SMBusDevice _controller;
        private Length _wheelDiameter;

        public EncoderMotorPort Port { get; }
        private byte RegisterControlMode => (byte)(0x60 + Port);
        private byte RegisterMode0Power => (byte)(0x64 + Port);
        private byte RegisterMode1Rpm => (byte)(0x68 + Port);
        private byte RegisterMode2RpmWithRotations => (byte)(0x6C + Port);
        private byte RegisterBrakeType => (byte)(0x70 + Port);
        private byte RegisterTachometer => (byte)(0x75 + Port);
        private byte RegisterOdometer => (byte)(0x7A + Port);
        public ForwardDirection ForwardDirection { get; set; }


        public BrakingType BrakingType
        {
            get => (BrakingType)_controller.ReadByte(RegisterBrakeType);
            set => _controller.WriteByte(RegisterBrakeType, (byte)value);
        }

        private byte ControlMode
        {
            get => _controller.ReadByte(RegisterControlMode);
            set => _controller.WriteByte(RegisterControlMode, value);
        }

        public double Power
        {
            get => ControlMode == 0 ? MapMotorPower(_controller.ReadWord(RegisterMode0Power)) : Double.NaN;
            set
            {
                ControlMode = 0;
                _controller.WriteWord(RegisterMode0Power, ToMotorPower(value));
            }
        }

        public Speed MaxSpeed => Speed.FromMetersPerSecond(WheelCircumference.Meters * MaxRpm.RevolutionsPerSecond);

        public RotationalSpeed MaxRpm => RotationalSpeed.FromRevolutionsPerMinute(((double)MAX_DC_MOTOR_RPM) / MMK_STANDARD_GEAR_RATIO);

        public Length WheelDiameter
        {
            get => _wheelDiameter;
            set
            {
                if (value.Meters <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(WheelDiameter), "Wheel Diameter cannot be 0 or negative");
                }
                _wheelDiameter = value;
                WheelCircumference = Length.FromMeters(_wheelDiameter.Meters * Math.PI);
            }
        }

        public Length WheelCircumference { get; private set; }

        public Speed Speed
        {
            get => Speed.FromMetersPerSecond(Rpm.RevolutionsPerSecond * WheelCircumference.Meters);
            set => ReachSpeed(value);
        }

        public RotationalSpeed Rpm
        {
            get => ControlMode == 0 ? RotationalSpeed.Zero : ReadRpm();
            set => ReachSpeed(value);
        }

        public Speed ActualSpeed => Speed.FromMetersPerSecond(ActualRpm.RevolutionsPerSecond * WheelCircumference.Meters);

        public RotationalSpeed ActualRpm => ControlMode == 0 ? RotationalSpeed.Zero : ReadActualRpm();

        public EncoderMotor(EncoderMotorPort port, SMBusDevice controller)
        {
            WheelDiameter = WheelDiameters.Standard;
            ForwardDirection = ForwardDirection.Clockwise;
            _controller = controller;
            Port = port;
        }

        public double RotationCount
        {
            get
            {
                var data = _controller.ReadBlock(RegisterOdometer);
                var count = BitConverter.ToInt32(data.Slice(0, 4)) * (int)ForwardDirection;
                return Math.Round((double) count / MMK_STANDARD_GEAR_RATIO, 1);
            }
        }

        public Length DistanceCount => Length.FromMeters(RotationCount * WheelCircumference.Meters);

        public void Stop()
        {
            switch (ControlMode)
            {
                case 0:
                    Power = 0;
                    break;
                case 1:
                case 2:
                    ReachSpeed(RotationalSpeed.Zero);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("ControlMode", "ControlMode can only be one of 0,1,2");

            }
        }

        private ushort ToMotorPower(in double value)
        {
            if (Math.Abs(value) > 1)
            {
                throw new ArgumentOutOfRangeException("Power", message: "Power values must be in the range [-1,1]");
            }
            var sign = (int)ForwardDirection;

            ushort power;
            unchecked
            {
                // todo, review the write and read of signed values
                power = (ushort)((int)Math.Round(value * 1000) * sign);
            }

            return power;
        }

        private double MapMotorPower(ushort value)
        {
            var sign = (int)ForwardDirection;
            return value / 1000.0 * sign;
        }

        private RotationalSpeed ReadRpm()
        {
            var sign = (int)ForwardDirection;
            var rpm = 0;
            switch (ControlMode)
            {
                case 1:
                    rpm = _controller.ReadWord(RegisterMode1Rpm);
                    break;

                case 2:
                    var data = _controller.ReadBlock(RegisterMode2RpmWithRotations);
                    // todo, review the write and read of signed values
                    rpm = BitConverter.ToUInt16(data.Slice(0, 2));
                    break;
            }
            return RotationalSpeed.FromRevolutionsPerMinute(((double)rpm) / (MMK_STANDARD_GEAR_RATIO * sign));

        }

        private RotationalSpeed ReadActualRpm()
        {
            var sign = (int)ForwardDirection;
            var rpm = 99999;
            
            while (Math.Abs(rpm) > MAX_DC_MOTOR_RPM)
            {
                rpm = _controller.ReadWord(RegisterTachometer);
            }

            return RotationalSpeed.FromRevolutionsPerMinute(((double)rpm) /(MMK_STANDARD_GEAR_RATIO * sign));
           
        }
        private void ReachSpeed(Speed speed)
        {
            var rpm = 60.0 * (speed.MetersPerSecond / WheelCircumference.Meters);
            ReachSpeed(RotationalSpeed.FromRevolutionsPerMinute(rpm));
        }

        private void ReachSpeed(RotationalSpeed speed)
        {
            ControlMode = 1;
            _controller.WriteWord(RegisterMode1Rpm, ToRpm(speed));
        }

        private ushort ToRpm(RotationalSpeed speed)
        {
            var sign = (int)ForwardDirection;

            ushort rpm;
            unchecked
            {
                // todo, review the write and read of signed values
                rpm = (ushort)((int)Math.Round(Math.Round(speed.RevolutionsPerMinute * MMK_STANDARD_GEAR_RATIO)) * sign);
            }

            return rpm;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Connect()
        {
            BrakingType = BrakingType.Coast;
            ControlMode = 0;
            Stop();
        }
    }
}