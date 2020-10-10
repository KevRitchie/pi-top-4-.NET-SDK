﻿using PiTop;
using PiTop.Camera;
using PiTop.MakerArchitecture.Expansion;
using PiTop.MakerArchitecture.Expansion.Rover;
using Pocket;

using SixLabors.ImageSharp;

using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

using UnitsNet;

namespace Prototype.App
{
    class Program
    {
        static void Main(string[] args)
        {
            LogEvents.Subscribe(i => Console.WriteLine(i.ToLogString()), new[]
            {
                //typeof(PiTop4Board).Assembly,
                //typeof(FoundationPlate).Assembly,
                //typeof(ExpansionPlate).Assembly,
                //typeof(RoverRobot).Assembly,
                //typeof(StreamingCamera).Assembly,
                //typeof(UltrasonicSensor).Assembly,
                typeof(Program).Assembly,
            });

            var js = new XboxController();

            // using ` mjpg_streamer -i "input_uvc.so -d /dev/video0" -o output_http.so`
            // ==> http://pi-top.local:8080/?action=stream
            PiTop4Board.Instance.UseCamera();
            using var rover = new RoverRobot(PiTop4Board.Instance.GetOrCreateExpansionPlate(), PiTop4Board.Instance.GetOrCreateCamera<StreamingCamera>(0),
                RoverRobotConfiguration.Default);
            var camControl = rover.TiltController;
            var motorControl = rover.MotionComponent as SteeringMotorController;

            rover.AllLightsOn();
            rover.BlinkAllLights();

            Observable.Interval(TimeSpan.FromMilliseconds(10))
                .Select(_ => (X: js.LeftStick.X, Y: js.LeftStick.Y))
                .DistinctUntilChanged()
                .Subscribe(stick =>
                {
                    var left = stick.X.WithDeadzone(-.5, .5, .3);
                    var forward = stick.Y;
                    motorControl.SetPower((forward + left) / 1.5, (forward - left) / 1.5);

                });

            js.Events.OfType<ButtonEvent>().Where(e => e.Button == Button.A)
                .Subscribe(e =>
                {
                    if (e.Pressed)
                    {
                        rover.AllLightsOn();
                    }
                    else
                    {
                        rover.AllLightsOff();
                    }
                });

            js.Events.OfType<ButtonEvent>().Where(e => e.Button == Button.X && e.Pressed)
                .Subscribe(e =>
                {
                    rover.Camera.GetFrame().Save("/home/pi/shot.jpg");
                });

            Observable.Interval(TimeSpan.FromMilliseconds(100))
                .Select(_ => (X: js.RightStick.X, Y: js.RightStick.Y))
                .DistinctUntilChanged()
                .Subscribe(stick =>
                {
                    camControl.SetSpeeds(
                        RotationalSpeed.FromRadiansPerSecond(stick.X / 3),
                        RotationalSpeed.FromRadiansPerSecond(stick.Y / 3)
                        );

                });

            js.Events.OfType<ButtonEvent>().Subscribe(e => Console.WriteLine($"Button: {e.Button} {e.Pressed}"));
            js.Events.OfType<ButtonEvent>().Where(e => e.Button == Button.RightStick)
                .Subscribe(e =>
                {
                    camControl.Reset();
                });

            Observable.Interval(TimeSpan.FromMilliseconds(100))
                .Select(_ =>
                new Unit[10].Select(_ => rover.UltrasoundFront.Distance.Centimeters).ToArray())
                .Subscribe(l =>
                {
                    var mean = l.Average();
                    var stddev = Math.Sqrt(l.Select(d => (d - mean) * (d - mean)).Average());
                    var maxrange = 1.5 * stddev;
                    var valid = l.Where(d => Math.Abs(d - mean) < maxrange).ToList();
                    if (valid.Count > 0)
                    {
                        Console.WriteLine($"Distance= {valid.Average():F1} cm ({valid.Count}: mean {mean:F1}, stddev {stddev:F1}, max {valid.Max():F1}, min {valid.Min():F1})");
                    }
                });

            Console.WriteLine("Ok, go drive around");
            Console.ReadKey();
            rover.AllLightsOff();
            rover.Dispose();
        }
    }
}
