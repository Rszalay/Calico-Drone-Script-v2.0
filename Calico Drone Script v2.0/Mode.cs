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
    public interface Mode
    {
        void Run();
        void Configure(string configString);
    }

    public class Escort : Mode
    {
        MyIni _ini;
        WcPbApi wcPbApi = new WcPbApi();
        //Controllers
        DroneCommsObject comms;
        MyThrustController thrustController;
        MyGyroController gyroController;
        MyObjectiveControllerD target;
        MyObjectiveControllerD center;
        MyObjectiveControllerD master;
        MyObjectiveControllerD obstacle;
        //Parts
        List<IMyCameraBlock> cameras;
        List<IMyTerminalBlock> allBlocks;
        //Settings
        double minSpeed;
        double kpObstacleAvoidence;
        double engageRange = 1000;
        //Other
        int count = 0;
        //Startup
        LaunchDelay delay;
        //objective data
        MyDetectedEntityInfo targetInfo;
        MyDetectedEntityInfo masterInfo;
        Vector3D centerPosition = Vector3D.Zero;
        long targetId;
        long masterId;
        //Self data
        Vector3D myVelocity;
        Vector3D myLastPosition; //do not use as data, use only in velocity calculation
        Vector3D myCurrentPosition;
        IMyGridTerminalSystem GridTerminalSystem;
        IMyProgrammableBlock Me;

        public Escort(IMyGridTerminalSystem gridTerminalSystem, IMyProgrammableBlock programmableBlock, IMyIntergridCommunicationSystem IGC)
        {
            GridTerminalSystem = gridTerminalSystem;
            Me = programmableBlock;
            wcPbApi.Activate(Me);
            string channel;
            _ini = new MyIni();
            MyIniParseResult result;
            if (!_ini.TryParse(Me.CustomData, out result))
                throw new Exception(result.ToString());
            channel = _ini.Get("Settings", "channel").ToString();
            comms = new DroneCommsObject(channel, IGC);
            _ini.Clear();
        }

        public void Configure(string configString)
        {
            double kpRotation, kiRotation, kdRotation;
            double kpThrust, kiThrust, kdThrust;
            double kpTargetRange, kiTargetRange, kdTargetRange;
            double kpCenterDistance, kiCenterDistance, kdCenterDistance;
            double kpMasterDistance, kiMasterDistance, kdMasterDistance;

            cameras.Clear();
            allBlocks.Clear();
            cameras = new List<IMyCameraBlock>();
            GridTerminalSystem.GetBlocksOfType(cameras);
            allBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(allBlocks);

            _ini = new MyIni();
            MyIniParseResult result;
            if (!_ini.TryParse(configString, out result))
                throw new Exception(result.ToString());

            kpRotation = _ini.Get("Escort", "kpRotation").ToDouble();
            kiRotation = _ini.Get("Escort", "kiRotation").ToDouble();
            kdRotation = _ini.Get("Escort", "kdRotation").ToDouble();
            gyroController = new MyGyroController(allBlocks, cameras.First(), kpRotation, kiRotation, kdRotation);
            kpThrust = _ini.Get("Escort", "kpThrust").ToDouble();
            kiThrust = _ini.Get("Escort", "kiThrust").ToDouble();
            kdThrust = _ini.Get("Escort", "kdThrust").ToDouble();
            thrustController = new MyThrustController(allBlocks, cameras.First(), kpThrust, kiThrust, kdThrust);
            kpTargetRange = _ini.Get("Escort", "kpTargetRange").ToDouble();
            kiTargetRange = _ini.Get("Escort", "kiTargetRange").ToDouble();
            kdTargetRange = _ini.Get("Escort", "kdTargetRange").ToDouble();
            target = new MyObjectiveControllerD(FeedBackType.Linear, kpTargetRange, kiTargetRange, kdTargetRange, 0);
            kpCenterDistance = _ini.Get("Escort", "kpCenterDistance").ToDouble();
            kiCenterDistance = _ini.Get("Escort", "kiCenterDistance").ToDouble();
            kdCenterDistance = _ini.Get("Escort", "kdCenterDistance").ToDouble();
            center = new MyObjectiveControllerD(FeedBackType.Exponential, kpCenterDistance, kiCenterDistance, kdCenterDistance, 0);
            kpMasterDistance = _ini.Get("Escort", "kpMasterDistance").ToDouble();
            kiMasterDistance = _ini.Get("Escort", "kiMasterDistance").ToDouble();
            kdMasterDistance = _ini.Get("Escort", "kdMasterDistance").ToDouble();
            master = new MyObjectiveControllerD(FeedBackType.Exponential, kpCenterDistance, kiCenterDistance, kdCenterDistance, 0);
            kpObstacleAvoidence = _ini.Get("Escort", "kpObstacleAvoidence").ToDouble();
            obstacle = new MyObjectiveControllerD(FeedBackType.Linear, kpObstacleAvoidence, 0, 0, 0);//Impliment later

            minSpeed = _ini.Get("Escort", "minSpeed").ToDouble();

            _ini.Clear();
        }

        public void Run()
        {
            thrustController.Tick();
            count++;
            int mod = count % 4;
            Vector3D navigationVector = GetNavigationVector();
            switch (mod)
            {
                case 0:
                    FireFixed();
                    break;
                case 1:
                    thrustController.Load(navigationVector, myVelocity, minSpeed);
                    thrustController.Run();
                    break;
                case 2:
                    gyroController.Load(navigationVector,new Vector3D(0,1,0));//impliment roll later
                    gyroController.Run();
                    break;
                case 3:
                    Communicate();
                    break;
                default:
                    break;
            }
            if(count >= 3) { count = 0; }
        }

        public void FireFixed()
        {

        }

        public void Thrust()
        {

        }

        public void Rotate()
        {

        }

        public void Communicate()
        {

        }

        public Vector3D GetNavigationVector()
        {
            double targetValue = target.Run(targetInfo.Position, myCurrentPosition);
            double centerValue = center.Run(centerPosition, myCurrentPosition);
            double masterValue = master.Run(masterInfo.Position, myCurrentPosition);
            double obstacleValue = 0; //implement later

            Vector3D navigationVector = Vector3D.Zero;
            navigationVector += targetValue * target.objectiveNorm;
            navigationVector += centerValue * center.objectiveNorm;
            navigationVector += masterValue * master.objectiveNorm;
            navigationVector += myVelocity;
            return Vector3D.Normalize(navigationVector);
        }
    }
}
