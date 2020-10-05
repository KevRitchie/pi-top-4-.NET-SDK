﻿using System;
using PiTop.MakerArchitecture.Foundation;
using PiTop.MakerArchitecture.Foundation.Components;
using PiTop.MakerArchitecture.Foundation.Sensors;
using SixLabors.ImageSharp;

namespace PiTop.MakerArchitecture.Expansion.Rover
{
    public class RoverRobot :IDisposable
    {
        public PanTiltController TiltController { get; }

        public SteeringMotorController MotorController { get; }

        public SoundSensor Sound { get; }

        public UltrasonicSensor UltrasoundBack { get; }

        public UltrasonicSensor UltrasoundFront { get; }

        public Led BackLeftLed { get; }

        public Led BackRightLed { get; }

        public Led FrontLeftLed { get; }

        public Led FrontRightLed { get; }

        public ExpansionPlate ExpansionPlate { get; }

        public RoverRobot(ExpansionPlate expansionPlate)
        {
            ExpansionPlate = expansionPlate ?? throw new ArgumentNullException(nameof(expansionPlate));

            TiltController = new PanTiltController(
                ExpansionPlate.GetOrCreateServoMotor(ServoMotorPort.S1),
                ExpansionPlate.GetOrCreateServoMotor(ServoMotorPort.S2)
                );

            MotorController = new SteeringMotorController(
                ExpansionPlate.GetOrCreateEncoderMotor(EncoderMotorPort.M3),
                ExpansionPlate.GetOrCreateEncoderMotor(EncoderMotorPort.M1)
                );

            FrontRightLed = ExpansionPlate.GetOrCreateLed(DigitalPort.D3, Color.Green);
            FrontLeftLed = ExpansionPlate.GetOrCreateLed(DigitalPort.D4, Color.Green);

            BackRightLed = ExpansionPlate.GetOrCreateLed(DigitalPort.D0, Color.Red);
            BackLeftLed = ExpansionPlate.GetOrCreateLed(DigitalPort.D5, Color.Red);

            UltrasoundFront = ExpansionPlate.GetOrCreateUltrasonicSensor(DigitalPort.D7);
            UltrasoundBack = ExpansionPlate.GetOrCreateUltrasonicSensor(DigitalPort.D6);

            Sound = ExpansionPlate.GetOrCreateSoundSensor(AnaloguePort.A3);

            FrontRightLed.Off();
            FrontLeftLed.Off();
            BackRightLed.Off();
            BackLeftLed.Off();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
