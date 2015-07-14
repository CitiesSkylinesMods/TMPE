using System.Collections.Generic;
using ColossalFramework;
using TrafficManager.CustomAI;
using TrafficManager.Traffic;

namespace TrafficManager.TrafficLight
{
    public class TrafficLightsTimed
    {
        public ushort nodeID;

        public static Dictionary<ushort, TrafficLightsTimed> timedScripts = new Dictionary<ushort, TrafficLightsTimed>(); 

        public List<TimedTrafficSteps> steps = new List<TimedTrafficSteps>();
        public int currentStep = 0;

        public List<ushort> nodeGroup; 

        public TrafficLightsTimed(ushort nodeID, List<ushort> nodeGroup)
        {
            this.nodeID = nodeID;
            this.nodeGroup = new List<ushort>(nodeGroup);

            CustomRoadAI.GetNodeSimulation(nodeID).TimedTrafficLightsActive = false;
        }

        public void addStep(int num)
        {
            steps.Add(new TimedTrafficSteps(num, nodeID));
        }

        public void start()
        {
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            currentStep = 0;
            steps[0].SetLights();
            steps[0].SetFrame(currentFrameIndex >> 6);

            CustomRoadAI.GetNodeSimulation(nodeID).TimedTrafficLightsActive = true;
        }

        public void moveStep(int oldPos , int newPos )
        {
            TimedTrafficSteps oldStep = steps[oldPos];

            steps.RemoveAt(oldPos);
            steps.Insert(newPos, oldStep);
        }

        public void stop()
        {
            CustomRoadAI.GetNodeSimulation(nodeID).TimedTrafficLightsActive = false;
        }

        public bool isStarted()
        {
            return CustomRoadAI.GetNodeSimulation(nodeID).TimedTrafficLightsActive;
        }

        public int NumSteps()
        {
            return steps.Count;
        }

        public TimedTrafficSteps GetStep(int stepID)
        {
            return steps[stepID];
        }

        public void checkStep(uint frame)
        {
            if (steps[currentStep].StepDone(frame))
            {
                currentStep = currentStep + 1 >= steps.Count ? 0 : currentStep + 1;

                steps[currentStep].SetFrame(frame);
                steps[currentStep].SetLights();
            }
        }

        public void skipStep()
        {
            uint currentFrameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex;

            currentStep = currentStep + 1 >= steps.Count ? 0 : currentStep + 1;

            steps[currentStep].SetFrame(currentFrameIndex >> 6);
            steps[currentStep].SetLights();
        }

        public long checkNextChange(int segmentID, int lightType)
        {
            var startStep = currentStep;
            var stepNum = currentStep + 1;
            var numFrames = steps[currentStep].CurrentStep();

            RoadBaseAI.TrafficLightState currentState;

            if (lightType == 0)
                currentState = TrafficLightsManual.GetSegmentLight(this.nodeID, segmentID).GetLightMain();
            else if (lightType == 1)
                currentState = TrafficLightsManual.GetSegmentLight(this.nodeID, segmentID).GetLightLeft();
            else if (lightType == 2)
                currentState = TrafficLightsManual.GetSegmentLight(this.nodeID, segmentID).GetLightRight();
            else
                currentState = TrafficLightsManual.GetSegmentLight(this.nodeID, segmentID).GetLightPedestrian();


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

                var light = steps[stepNum].GetLight(segmentID, lightType);

                if (light != currentState)
                {
                    break;
                }
                else
                {
                    numFrames += steps[stepNum].NumSteps;
                }

                stepNum++;
            }

            return numFrames;
        }

        public void resetSteps()
        {
            steps.Clear();
        }

        public void RemoveStep(int id)
        {
            steps.RemoveAt(id);
        }

        public static void AddTimedLight(ushort nodeid, List<ushort> nodeGroup)
        {
            timedScripts.Add(nodeid, new TrafficLightsTimed(nodeid, nodeGroup));
        }

        public static void RemoveTimedLight(ushort nodeid)
        {
            timedScripts.Remove(nodeid);
        }

        public static bool IsTimedLight(ushort nodeid)
        {
            return timedScripts.ContainsKey(nodeid);
        }

        public static TrafficLightsTimed GetTimedLight(ushort nodeid)
        {
            return timedScripts[nodeid];
        }
    }
}
