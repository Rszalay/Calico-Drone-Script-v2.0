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
        MyDetectedEntityInfo currentTarget;
        long targetId;
        //Self data
        Vector3D myVelocity;
        Vector3D myLastPosition; //do not use as data, use only in velocity calculation
        Vector3D myCurrentPosition;
        IMyGridTerminalSystem GridTerminalSystem;
        IMyProgrammableBlock Me;

        public Escort(IMyGridTerminalSystem gridTerminalSystem, IMyProgrammableBlock programmableBlock)
        {
            GridTerminalSystem = gridTerminalSystem;
            Me = programmableBlock;
        }

        public void Configure(string configString)
        {
            double kpRotation, kiRotation, kdRotation;
            double kpThrust, kiThrust, kdThrust;
            double kpTargetRange, kiTargetRange, kdTargetRange;
            double kpCenterDistance, kiCenterDistance, kdCenterDistance;
            double kpMasterDistance, kiMasterDistance, kdMasterDistance;

            cameras = new List<IMyCameraBlock>();
            GridTerminalSystem.GetBlocksOfType(cameras);
            allBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(allBlocks);

            _ini = new MyIni();
            MyIniParseResult result;
            if (!_ini.TryParse(configString, out result))
                throw new Exception(result.ToString());

            kpRotation = _ini.Get("Gains", "kpRotation").ToDouble();
            kiRotation = _ini.Get("Gains", "kiRotation").ToDouble();
            kdRotation = _ini.Get("Gains", "kdRotation").ToDouble();
            gyroController = new MyGyroController(allBlocks, cameras.First(), kpRotation, kiRotation, kdRotation);
            kpThrust = _ini.Get("Gains", "kpThrust").ToDouble();
            kiThrust = _ini.Get("Gains", "kiThrust").ToDouble();
            kdThrust = _ini.Get("Gains", "kdThrust").ToDouble();
            thrustController = new MyThrustController(allBlocks, cameras.First(), kpThrust, kiThrust, kdThrust);
            kpTargetRange = _ini.Get("Gains", "kpTargetRange").ToDouble();
            kiTargetRange = _ini.Get("Gains", "kiTargetRange").ToDouble();
            kdTargetRange = _ini.Get("Gains", "kdTargetRange").ToDouble();
            target = new MyObjectiveControllerD(FeedBackType.Linear, kpTargetRange, kiTargetRange, kdTargetRange, 0);
            kpCenterDistance = _ini.Get("Gains", "kpCenterDistance").ToDouble();
            kiCenterDistance = _ini.Get("Gains", "kiCenterDistance").ToDouble();
            kdCenterDistance = _ini.Get("Gains", "kdCenterDistance").ToDouble();
            center = new MyObjectiveControllerD(FeedBackType.Exponential, kpCenterDistance, kiCenterDistance, kdCenterDistance, 0);
            kpMasterDistance = _ini.Get("Gains", "kpMasterDistance").ToDouble();
            kiMasterDistance = _ini.Get("Gains", "kiMasterDistance").ToDouble();
            kdMasterDistance = _ini.Get("Gains", "kdMasterDistance").ToDouble();
            master = new MyObjectiveControllerD(FeedBackType.Exponential, kpCenterDistance, kiCenterDistance, kdCenterDistance, 0);
            kpObstacleAvoidence = _ini.Get("Gains", "kpObstacleAvoidence").ToDouble();
            obstacle = new MyObjectiveControllerD(FeedBackType.Linear, kpObstacleAvoidence, 0, 0, 0);

            minSpeed = _ini.Get("Settings", "minSpeed").ToDouble();

            _ini.Clear();
        }

        public void Run()
        {
            thrustController.Tick();
            count++;
            int mod = count % 4;
            switch (mod)
            {
                case 0:
                    FireFixed();
                    break;
                case 1:
                    Thrust();
                    break;
                case 2:
                    Rotate();
                    break;
                case 3:
                    Communicate();
                    break;
                default:
                    break;
            }
        }
    }
}
