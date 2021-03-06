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
        void Configure(string configString, object goal = null);
    }

    public class Escort : Mode
    {
        public readonly string modeName = "Escort";
        MyIni _ini;
        WcPbApi wcPbApi = new WcPbApi();
        //Controllers
        DroneCommsObject comms;
        MyThrustController thrustController;
        MyGyroController gyroController;
        MyObjectiveControllerD target;
        MyObjectiveControllerD center;
        MyObjectiveControllerD master;
        //Parts
        List<IMyCameraBlock> cameras;
        List<IMyTerminalBlock> allBlocks;
        IMyTerminalBlock camera;
        //Settings
        double speed;
        double engageRange = 1000;
        //Other
        int count = 0;
        //objective data
        Vector3D centerPosition = Vector3D.Zero;
        ContactTracker targetContact;
        ContactTracker masterContact;
        TargetAnalyzer targetAnalyzer;
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

        public void Configure(string configString, object goal = null)
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
            camera = cameras.First();
            allBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(allBlocks);

            _ini = new MyIni();
            MyIniParseResult result;
            if (!_ini.TryParse(configString, out result))
                throw new Exception(result.ToString());

            kpRotation = _ini.Get("Escort", "kpRotation").ToDouble();
            kiRotation = _ini.Get("Escort", "kiRotation").ToDouble();
            kdRotation = _ini.Get("Escort", "kdRotation").ToDouble();
            gyroController = new MyGyroController(allBlocks, camera, kpRotation, kiRotation, kdRotation);
            kpThrust = _ini.Get("Escort", "kpThrust").ToDouble();
            kiThrust = _ini.Get("Escort", "kiThrust").ToDouble();
            kdThrust = _ini.Get("Escort", "kdThrust").ToDouble();
            thrustController = new MyThrustController(allBlocks, camera, kpThrust, kiThrust, kdThrust);
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
            List<double> targetValueGains = new List<double>();
            targetValueGains[0] = _ini.Get("TargetSelection", "targetDistance").ToDouble();
            targetValueGains[1] = _ini.Get("TargetSelection", "centerDistance").ToDouble();
            targetValueGains[2] = _ini.Get("TargetSelection", "masterDistance").ToDouble();
            targetValueGains[3] = _ini.Get("TargetSelection", "targetSize").ToDouble();
            targetAnalyzer = new TargetAnalyzer(targetValueGains);

            speed = _ini.Get("Escort", "minSpeed").ToDouble();

            _ini.Clear();
        }

        public void Run()
        {
            thrustController.Tick();
            count++;
            Vector3D navigationVector = GetNavigationVector();
            Dictionary<MyDetectedEntityInfo, float> threats = new Dictionary<MyDetectedEntityInfo, float>();
            wcPbApi.GetSortedThreats(Me, threats);
            List<MyDetectedEntityInfo> contacts = new List<MyDetectedEntityInfo>();
            foreach(var threat in threats) { contacts.Add(threat.Key); }
            if (!targetContact.Update(contacts)) { SelectTarget(contacts); }
            masterContact.Update(contacts);
            switch (count)
            {
                case 1:
                    FireFixed();
                    break;
                case 2:
                    Thrust(navigationVector);
                    break;
                case 3:
                    Rotate(navigationVector);
                    break;
                case 4:
                    Communicate(contacts);
                    break;
                default:
                    break;
            }
            if(count >= 4) { count = 0; }
            threats.Clear();
            contacts.Clear();
        }

        public void FireFixed()
        {

        }

        public void Thrust(Vector3D navigationVector)
        {
            thrustController.Load(navigationVector, myVelocity, speed);
            thrustController.Run();
        }

        public void Rotate(Vector3D navigationVector)
        {
            gyroController.Load(navigationVector,new Vector3D(0,1,0));//implement roll later
            gyroController.Run();
        }

        public void Communicate(List<MyDetectedEntityInfo> contacts)
        {
            foreach(var message in comms.Messages)
            {
                if(message.Split(';')[0] == "SetMaster")
                {
                    long newMasterId;
                    long.TryParse(message.Split(';')[1], out newMasterId);
                    foreach (var contact in contacts)
                    {
                        if(contact.EntityId == newMasterId) { masterContact = new ContactTracker(contact); }
                    }
                }
                else if (message.Split(';')[0] == "SetTarget")
                {
                    long newTargetId;
                    long.TryParse(message.Split(';')[1], out newTargetId);
                    foreach (var contact in contacts)
                    {
                        if (contact.EntityId == newTargetId) { masterContact = new ContactTracker(contact); }
                    }
                }
            }
        }

        public void SelectTarget(List<MyDetectedEntityInfo> contacts)
        {
            List<Vector3D> objectives = new List<Vector3D>();
            objectives[0] = myCurrentPosition;
            objectives[1] = centerPosition;
            objectives[2] = masterContact.contactCurrentPosition;
            MyDetectedEntityInfo newTarget = targetAnalyzer.AnalyzeAll(contacts, objectives);
            targetContact = new ContactTracker(newTarget);
            objectives.Clear();
        }

        public Vector3D GetNavigationVector()
        {
            target.Set(engageRange);
            center.Set(9000);//change me
            master.Set(2000);//change me

            myCurrentPosition = camera.WorldMatrix.Translation;
            myVelocity = myCurrentPosition - myLastPosition;
            myLastPosition = myCurrentPosition;

            double targetValue = target.Run(targetContact.contactCurrentPosition, myCurrentPosition);
            double centerValue = center.Run(centerPosition, myCurrentPosition);
            double masterValue = master.Run(masterContact.contactCurrentPosition, myCurrentPosition);

            Vector3D navigationVector = Vector3D.Zero;
            if(targetContact.contactId != 0)navigationVector += targetValue * target.objectiveNorm;
            navigationVector += centerValue * center.objectiveNorm;
            if (masterContact.contactId != 0) navigationVector += masterValue * master.objectiveNorm;
            navigationVector += myVelocity;
            return Vector3D.Normalize(navigationVector);
        }
    }

    public class ModeLoader
    {
        Dictionary<string, Mode> operatingModes = new Dictionary<string, Mode>();

        public Dictionary<string, Mode> Load(string configString)
        {
            MyIni _ini;
            List<MyIniKey> iniKeys = new List<MyIniKey>();
            List<string> modesToLoad = new List<string>();
            _ini = new MyIni();
            MyIniParseResult result;
            if (!_ini.TryParse(configString, out result))
                throw new Exception(result.ToString());

            _ini.GetKeys(configString, iniKeys);
            foreach(var key in iniKeys)
            {
                string name = "";
                if(key.Name.Split()[0] == "Mode") { name = key.Name.Split()[1]; }
                if (name != "") { modesToLoad.Add(name)};
            }
            foreach(var mode in modesToLoad)
            {

            }
        }
    }
}
