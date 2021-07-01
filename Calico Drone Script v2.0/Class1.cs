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
    public class TargetTracker
    {
        public long targetId { get; private set; }
        public Vector3D position { get; private set; }
        public Vector3D velocity { get; private set; }
        List<MyDetectedEntityInfo> _contacts;
        List<string> _messages;

        public virtual void Update(List<string> messages, List<MyDetectedEntityInfo> contacts)
        {
            _contacts.Clear();
            _contacts = contacts;
            _messages.Clear();
            _messages = messages;
        }
    }

    public class Hivemind : TargetTracker
    {
        
    }

    public class Screen : TargetTracker
    {

    }

    public class TargetAnalyzer
    {
        double _targetDistance;
        double _centerDistance;
        double _masterDistance;
        double _targetSize;

        public TargetAnalyzer(List<double> targetValueGains)
        {
            _targetDistance = targetValueGains[0];
            _centerDistance = targetValueGains[1];
            _masterDistance = targetValueGains[2];
            _targetSize = targetValueGains[3];
        }

        public double AnalyzeSingle(MyDetectedEntityInfo contact, List<Vector3D> objectivePositions)
        {
            double value = 0;
            value += (contact.Position - objectivePositions[0]).Length() * _targetDistance;
            value += (contact.Position - objectivePositions[1]).Length() * _centerDistance;
            value += (contact.Position - objectivePositions[2]).Length() * _masterDistance;
            value += contact.BoundingBox.Volume * _targetSize;
            return value;
        }

        public MyDetectedEntityInfo AnalyzeAll(List<MyDetectedEntityInfo> contacts, List<Vector3D> objectivePositions)
        {
            MyDetectedEntityInfo bestContact = new MyDetectedEntityInfo();
            double bestValue = 0;
            foreach(var contact in contacts)
            {
                if (contact.Relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
                {
                    double value = AnalyzeSingle(contact, objectivePositions);
                    if(value > bestValue)
                    {
                        bestContact = contact;
                        bestValue = value;
                    }
                }
            }
            return bestContact;
        }
    }
}
