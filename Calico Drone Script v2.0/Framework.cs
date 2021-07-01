using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    class MyThrustController
    {
        Vector3D _pv, _sp, currentPosition, lastPosition, velocitySp;
        public Vector3D velocityPv { get; private set; }
        Ideal ConX, ConY, ConZ;
        IMyTerminalBlock forwardReferenceBlock;
        public Dictionary<Base6Directions.Direction, List<IMyThrust>> thrusterSet;
        Program _myProgram;
        double xThrottle;
        double yThrottle;
        double zThrottle;
        double speedSp;
        int ticksSinceLastRun = 0;

        public MyThrustController(List<IMyTerminalBlock> blocks, IMyTerminalBlock forwardBlock, double kp, double ki, double kd)
        {
            ConX = new Ideal(kp, ki, kd, 2);
            ConY = new Ideal(kp, ki, kd, 2);
            ConZ = new Ideal(kp, ki, kd, 2);
            forwardReferenceBlock = forwardBlock;
            thrusterSet = new Dictionary<Base6Directions.Direction, List<IMyThrust>>();
            foreach (IMyTerminalBlock block in blocks)
            {
                if (block is IMyThrust)
                {
                    if (!thrusterSet.ContainsKey(block.Orientation.Forward))
                    {
                        thrusterSet.Add(block.Orientation.Forward, new List<IMyThrust>());
                        thrusterSet[block.Orientation.Forward].Add(block as IMyThrust);
                    }
                    else { thrusterSet[block.Orientation.Forward].Add(block as IMyThrust); }
                }
            }
        }

        public void Load(Vector3D sp, Vector3D pv, double speed)
        {
            speedSp = speed;
            _pv = pv;
            _sp = sp;
            currentPosition = forwardReferenceBlock.WorldMatrix.Translation;
            Vector3D error = _sp - _pv;
            velocitySp = Vector3D.Normalize(error) * speedSp;
            velocityPv = (currentPosition - lastPosition) * (60.0 / ticksSinceLastRun);
            error = (velocitySp - velocityPv);
            _myProgram.Echo(error.Z.ToString());
            //Vector3D referenceWorldPosition = forwardReferenceBlock.WorldMatrix.Translation;
            //Vector3D worldDirection = error - referenceWorldPosition;
            //Vector3D errorInGridFrame = Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(forwardReferenceBlock.WorldMatrix));
            ConX.Load(0, error.X);
            ConY.Load(0, error.Y);
            ConZ.Load(0, error.Z);
            lastPosition = currentPosition;
            ticksSinceLastRun = 0;
        }

        public void Run()
        {
            xThrottle = ConX.Run();
            yThrottle = ConY.Run();
            zThrottle = ConZ.Run();
            Vector3D thrustVector = new Vector3D(xThrottle, yThrottle, zThrottle);
            foreach (var orientation in thrusterSet)
            {
                var first = orientation.Value.First();
                if (orientation.Value.Count() > 0)//should probably sort by throttle settings first so you don't have to check orientations on thruster that should be off
                {
                    Vector3D thrusterWF = first.WorldMatrix.Forward;

                    double thrusterDot = Vector3D.Dot(first.WorldMatrix.Backward, thrustVector);
                    if (thrusterDot > 0)
                    {
                        foreach (var thruster in orientation.Value) { thruster.ThrustOverride = (float)thrusterDot; }
                    }
                    else
                    {
                        foreach (var thruster in orientation.Value) { thruster.ThrustOverride = 0; }
                    }
                }
            }
        }

        public void Tick() { ticksSinceLastRun++; }
    }

    public class MyObjectiveControllerD
    {
        Vector3D _objectiveCurrentPosition, _objectiveLastPosition;
        Vector3D _myCurrentPosition;
        Ideal _myIdeal;
        FeedBack _feedBack;
        double _setPoint = 0;
        public double output { get; private set; } = 0;
        public Vector3D objectiveNorm { get; private set; } = Vector3D.Zero;
        public Vector3D objectiveVelocity { get; private set; } = Vector3D.Zero;

        public MyObjectiveControllerD(FeedBackType feedBack, double kp, double ki, double kd, double iClamp)
        {
            if (feedBack == FeedBackType.Linear) { _feedBack = new Linear(); }
            if (feedBack == FeedBackType.Cubic) { _feedBack = new Cubic(); }
            if (feedBack == FeedBackType.Exponential) { _feedBack = new Exponential(); }
            _myIdeal = new Ideal(kp, ki, kd, iClamp);
        }

        public double Run(Vector3D objectivePosition, Vector3D myPosition)
        {
            _myCurrentPosition = myPosition;
            _objectiveCurrentPosition = objectivePosition;
            objectiveVelocity = _objectiveCurrentPosition - _objectiveLastPosition;
            _objectiveLastPosition = _objectiveCurrentPosition;
            objectiveNorm = Vector3D.Normalize(_objectiveCurrentPosition - _myCurrentPosition);

            double magnitude = (_objectiveCurrentPosition - _myCurrentPosition).Length();
            _myIdeal.Load(_setPoint, _feedBack.Run(magnitude));
            output = _myIdeal.Run();
            return output;
        }

        public void Reset(double sp)
        {
            //call when changing targets
            _myIdeal.Reset();
            objectiveVelocity = Vector3D.Zero;
            _objectiveLastPosition = Vector3D.Zero;
            _setPoint = sp;
        }
        
        public void Set(double sp) { _setPoint = sp; }//call when changing setpoint
    }

    public enum FeedBackType : int
    {
        Linear = 0,
        Cubic = 1,
        Exponential = 2
    }

    public interface FeedBack
    {
        double Run(double pv);
    }

    public class Linear : FeedBack
    {
        public double Run(double pv)
        {
            return pv;
        }
    }

    public class Cubic : FeedBack
    {
        public double Run(double pv)
        {
            return pv * pv * pv;
        }
    }

    public class Exponential : FeedBack
    {
        public double Run(double pv)
        {
            double result = Math.Pow(MathHelperD.E, pv);
            if (result > 100000) { return 100000; }
            return result;
        }
    }

    public class MyGyroController
    {
        public Dictionary<MyBlockOrientation, List<IMyGyro>> gyroSets;
        IMyTerminalBlock forwardReferenceBlock;
        Vector3D _tangentVector;
        Vector3D _normalVector;
        Ideal Yaw, Pitch, Roll;

        public MyGyroController(List<IMyTerminalBlock> blocks, IMyTerminalBlock forwardBlock, double kp, double ki, double kd)
        {
            Yaw = new Ideal(kp, ki, kd, 0);
            Pitch = new Ideal(kp, ki, kd, 0);
            Roll = new Ideal(kp, ki, kd, 0);
            forwardReferenceBlock = forwardBlock;
            gyroSets = new Dictionary<MyBlockOrientation, List<IMyGyro>>();
            foreach (IMyTerminalBlock block in blocks)
            {
                if (block is IMyGyro)
                {
                    if (!gyroSets.ContainsKey(block.Orientation))
                    {
                        gyroSets.Add(block.Orientation, new List<IMyGyro>());
                        gyroSets[block.Orientation].Add(block as IMyGyro);
                    }
                    else { gyroSets[block.Orientation].Add(block as IMyGyro); }
                }
            }
        }

        public void ApplyGyroOverride(Vector3D gyroRpms)
        {
            //Modified from Whip's ApplyGyroOverride Method v9 - 8/19/17
            // X : Pitch, Y : Yaw, Z : Roll
            //gyroRpms.X = -gyroRpms.X;
            var relativeRotationVec = Vector3D.TransformNormal(gyroRpms, forwardReferenceBlock.WorldMatrix);
            foreach (var orientation in gyroSets)
            {
                var firstGyro = orientation.Value.First();
                var transformedRotationVec = Vector3D.TransformNormal(relativeRotationVec, Matrix.Transpose(firstGyro.WorldMatrix));
                foreach (var gyro in orientation.Value)
                {
                    gyro.Pitch = (float)transformedRotationVec.X;
                    gyro.Yaw = (float)transformedRotationVec.Y;
                    gyro.Roll = (float)transformedRotationVec.Z;
                    gyro.GyroOverride = true;
                }
            }
        }

        public void Load(Vector3D tangentVector, Vector3D nearNormalVector)
        {
            _tangentVector = tangentVector;
            _normalVector = nearNormalVector;
            Vector3D biTangentVector = Vector3D.Cross(tangentVector, nearNormalVector);
            _normalVector = Vector3D.Cross(tangentVector, biTangentVector);

            Vector3D transformedTangent = Vector3D.TransformNormal(_tangentVector, MatrixD.Transpose(forwardReferenceBlock.WorldMatrix));
            Vector3D transformedNormal = Vector3D.TransformNormal(_normalVector, MatrixD.Transpose(forwardReferenceBlock.WorldMatrix));

            double pitchError = Math.Asin(-transformedTangent.Y);
            double YawError = Math.Asin(transformedTangent.X);
            double rollError = Math.Asin(-transformedNormal.X);

            Yaw.Load(0, YawError);
            Pitch.Load(0, pitchError);
            Roll.Load(0, rollError);
        }

        public void Run()
        {
            Vector3D gyroRpms = Vector3D.Zero;
            gyroRpms.X = Pitch.Run();
            gyroRpms.Y = Yaw.Run();
            gyroRpms.Z = Roll.Run();
            ApplyGyroOverride(gyroRpms);
        }
    }

    public interface IMyController
    {
        void Load(double sp, double vp);
        double Run(double error);
        double Run();
        double Output();
        void Reset();
    }

    public class Proportional : IMyController
    {
        double _kp, _error, _output;

        public void Load(double sp, double vp) { _error = vp - sp; }

        public Proportional(double kp) { _kp = kp; }

        public double Run(double error) { _output = error * _kp; return _output; }

        public double Run() { return this.Run(_error); }

        public double Output() { return _output; }

        public void Reset() { }
    }

    public class Integral : IMyController
    {
        double _ki, _acc, _iClamp, _error, _output;

        public void Load(double sp, double vp) { _error = vp - sp; }

        public Integral(double ki, double iClamp) { _ki = ki; _iClamp = iClamp; }

        public double Run(double error)
        {
            if (error < _iClamp) { _acc += error; }
            _output = _acc * _ki;
            return _output;
        }

        public double Run() { return this.Run(_error); }

        public double Output() { return _output; }

        public void Reset() { _acc = 0; }
    }

    public class Derivative : IMyController
    {
        double _kd, _last, _error, _output;

        public void Load(double sp, double vp) { _error = vp - sp; }

        public Derivative(double kd) { _kd = kd; }

        public double Run(double error)
        {
            double derivative = (error - _last) * _kd;
            _last = error;

            _output = derivative;
            return derivative;
        }

        public double Run() { return this.Run(_error); }

        public double Output() { return _output; }

        public void Reset() { _last = 0; }
    }

    public class Ideal : IMyController
    {
        List<IMyController> controllers = new List<IMyController>();
        Proportional proportional;
        double _error, _output;

        public void Load(double sp, double vp) { _error = vp - sp; }

        public Ideal(double kp, double ki, double kd, double iClamp)
        {
            controllers.Add(new Integral(ki, iClamp));
            controllers.Add(new Derivative(kd));
            proportional = new Proportional(kp);
        }

        public double Run(double error)
        {
            double vp_kp = proportional.Run(error);
            double acc = vp_kp;
            foreach (IMyController controller in controllers) { acc += controller.Run(vp_kp); }
            _output = acc;
            return acc;
        }

        public double Output() { return _output; }

        public double Run() { return this.Run(_error); }

        public void Reset() { foreach (IMyController controller in controllers) { controller.Reset(); } }
    }

    public class DroneCommsObject
    {
        IMyIntergridCommunicationSystem _IGC;
        string _broadcastTag = "";
        public List<string> Messages = new List<string>();

        public DroneCommsObject(string broadcastTag, IMyIntergridCommunicationSystem IGC) { _broadcastTag = broadcastTag; _IGC = IGC; }

        public string RecieveMessages()
        {
            //from Malware / Wicorel: IGC-Example-1-Simple-Echo-Example
            //update the list of messages, if the user only wants message 1 return that
            IMyBroadcastListener _myBroadcastListener;
            _myBroadcastListener = _IGC.RegisterBroadcastListener(_broadcastTag);
            _myBroadcastListener.SetMessageCallback(_broadcastTag);
            while (_myBroadcastListener.HasPendingMessage)
            {
                MyIGCMessage myIGCMessage = _myBroadcastListener.AcceptMessage();
                if (myIGCMessage.Tag == _broadcastTag)
                { // This is our tag
                    if (myIGCMessage.Data is string)
                    {
                        if (myIGCMessage.Data.ToString() != "") { Messages.Add(myIGCMessage.Data.ToString()); }
                    }
                }
            }
            if (Messages.Count > 0) { return Messages.First(); }
            else { return ""; }
        }

        public void SendMessage(string _message)
        {
            //from Malware / Wicorel: IGC-Example-1-Simple-Echo-Example
            if (_message != "")
            {
                _IGC.SendBroadcastMessage(_broadcastTag, _message);
            }
        }

        public void ClearMessages()
        {
            Messages.Clear();
        }
    }
}
