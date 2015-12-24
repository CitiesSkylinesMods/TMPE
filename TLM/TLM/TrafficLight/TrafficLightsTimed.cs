using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.CustomAI;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight
{
    public class TrafficLightsTimed
    {
        public ushort NodeId;

        public static Dictionary<ushort, TrafficLightsTimed> TimedScripts = new Dictionary<ushort, TrafficLightsTimed>();

        public List<TimedTrafficSteps> Steps = new List<TimedTrafficSteps>();
        public int CurrentStep;

        public List<ushort> NodeGroup;

        public TrafficLightsTimed(ushort nodeId, IEnumerable<ushort> nodeGroup)
        {
            NodeId = nodeId;
            NodeGroup = new List<ushort>(nodeGroup);

            CustomRoadAI.GetNodeSimulation(nodeId).TimedTrafficLightsActive = false;
        }

        public void AddStep(int timeUnits)
        {
            Steps.Add(new TimedTrafficSteps(timeUnits, NodeId));
        }

        public void Start()
        {
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            CurrentStep = 0;
            Steps[0].SetLights();
            Steps[0].SetFrame(currentFrameIndex >> 6);

            CustomRoadAI.GetNodeSimulation(NodeId).TimedTrafficLightsActive = true;
        }

        public void MoveStep(int oldPos , int newPos )
        {
            var oldStep = Steps[oldPos];

            Steps.RemoveAt(oldPos);
            Steps.Insert(newPos, oldStep);
        }

        public void Stop()
        {
            CustomRoadAI.GetNodeSimulation(NodeId).TimedTrafficLightsActive = false;
        }

        public bool IsStarted()
        {
            return CustomRoadAI.GetNodeSimulation(NodeId).TimedTrafficLightsActive;
        }

        public int NumSteps()
        {
            return Steps.Count;
        }

        public TimedTrafficSteps GetStep(int stepId)
        {
            return Steps[stepId];
        }

        public void CheckStep(uint frame)
        {
            if (!Steps[CurrentStep].StepDone(frame)) return;

            CurrentStep = (CurrentStep + 1) % Steps.Count;

            Steps[CurrentStep].SetFrame(frame);
            Steps[CurrentStep].SetLights();
        }

        public void SkipStep()
        {
            var currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            CurrentStep = CurrentStep + 1 >= Steps.Count ? 0 : CurrentStep + 1;

            Steps[CurrentStep].SetFrame(currentFrameIndex >> 6);
            Steps[CurrentStep].SetLights();
        }

        public long CheckNextChange(int segmentId, int lightType)
        {
            var startStep = CurrentStep;
            var stepNum = CurrentStep + 1;
            var numFrames = Steps[CurrentStep].CurrentStep();

            RoadBaseAI.TrafficLightState currentState;

            if (lightType == 0)
                currentState = TrafficLightsManual.GetSegmentLight(NodeId, segmentId).GetLightMain();
            else if (lightType == 1)
                currentState = TrafficLightsManual.GetSegmentLight(NodeId, segmentId).GetLightLeft();
            else if (lightType == 2)
                currentState = TrafficLightsManual.GetSegmentLight(NodeId, segmentId).GetLightRight();
            else
                currentState = TrafficLightsManual.GetSegmentLight(NodeId, segmentId).GetLightPedestrian();


            while (true)
            {
                if (stepNum > NumSteps() - 1)
                {
                    stepNum = 0;
                }

                if (stepNum == startStep)
                {
                    numFrames = 99;
                    break;
                }

                var light = Steps[stepNum].GetLight(segmentId, lightType);

                if (light != currentState)
                {
                    break;
                }
                else
                {
                    numFrames += Steps[stepNum].timeUnits;
                }

                stepNum++;
            }

            return numFrames;
        }

        public void ResetSteps()
        {
            Steps.Clear();
        }

        public void RemoveStep(int id)
        {
            Steps.RemoveAt(id);
        }

        public static void AddTimedLight(ushort nodeid, List<ushort> nodeGroup)
        {
            TimedScripts.Add(nodeid, new TrafficLightsTimed(nodeid, nodeGroup));
        }

        public static void RemoveTimedLight(ushort nodeid)
        {
            TimedScripts.Remove(nodeid);
        }

        public static bool IsTimedLight(ushort nodeid)
        {
            return TimedScripts.ContainsKey(nodeid);
        }

        public static TrafficLightsTimed GetTimedLight(ushort nodeid)
        {
			if (!IsTimedLight(nodeid))
				return null;
            return TimedScripts[nodeid];
        }
    }
}
