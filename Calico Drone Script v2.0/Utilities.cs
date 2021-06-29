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
    class LaunchDelay
    {
        int delay = 0;
        int _spread = 600;
        bool goPermission = false;
        bool ready = false;
        public bool run { get; private set; } = false;
        DroneCommsObject _comms;
        string _launchCode = "go";
        long _thisDroneEntityID;

        public LaunchDelay(int spread, DroneCommsObject comms, long thisDroneEntityID, string launchCode)
        {
            _spread = spread;
            _comms = comms;
            _launchCode = launchCode;
            _thisDroneEntityID = thisDroneEntityID;
        }

        public void Launch(string launchCode)
        {
            goPermission = true;
            _comms.SendMessage("start;" + launchCode);
        }

        public void Tick()
        {
            if (ready)
            {
                if(delay <= 0) { run = true; return; }
                delay--;
            }
            else if (goPermission)
            {
                List<long> droneIdsOnNetwork = new List<long>();
                droneIdsOnNetwork.Add(_thisDroneEntityID);
                foreach(var message in _comms.Messages)
                {
                    if(message.Split(';')[0] == "ID") 
                    {
                        long ID;
                        long.TryParse(message.Split(';')[1], out ID);
                        droneIdsOnNetwork.Add(ID); 
                    }
                }
                droneIdsOnNetwork.Sort();
                int launchNumber = droneIdsOnNetwork.FindIndex(x => x.Equals(_thisDroneEntityID));
                delay = (_spread / droneIdsOnNetwork.Count()) * launchNumber;
                ready = true;
                droneIdsOnNetwork.Clear();
                _comms.ClearMessages();
            }
            else if (!goPermission)
            {
                foreach(var message in _comms.Messages)
                {
                    if (message.Split(';')[0] == "Start" && message.Split(';')[0] == _launchCode)
                    {
                        _comms.ClearMessages();
                        goPermission = true;
                        break;
                    }
                }
            }
        }
    }
}
