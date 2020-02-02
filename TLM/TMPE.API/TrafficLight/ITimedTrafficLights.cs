namespace TrafficManager.API.TrafficLight {
    using System.Collections.Generic;
    using CSUtil.Commons;
    using TrafficManager.API.Traffic.Enums;

    public interface ITimedTrafficLights {
        IDictionary<ushort, IDictionary<ushort, ArrowDirection>> Directions { get; }
        ushort NodeId { get; }
        ushort MasterNodeId { get; set; } // TODO private set
        short RotationOffset { get; }
        int CurrentStep { get; set; }
        bool TestMode { get; set; } // TODO private set
        IList<ushort> NodeGroup { get; set; } // TODO private set

        ITimedTrafficLightsStep AddStep(int minTime,
                                        int maxTime,
                                        StepChangeMetric changeMetric,
                                        float waitFlowBalance,
                                        bool makeRed = false);

        long CheckNextChange(ushort segmentId,
                             bool startNode,
                             ExtVehicleType vehicleType,
                             int lightType);

        ITimedTrafficLightsStep GetStep(int stepId);
        bool Housekeeping(); // TODO improve & remove
        bool IsMasterNode();
        bool IsStarted();
        bool IsInTestMode();
        void SetTestMode(bool testMode);
        void Destroy();
        ITimedTrafficLights MasterLights();
        void MoveStep(int oldPos, int newPos);
        int NumSteps();
        void RemoveStep(int id);
        void ResetSteps();
        void RotateLeft();
        void RotateRight();
        void Join(ITimedTrafficLights otherTimedLight);
        void PasteSteps(ITimedTrafficLights sourceTimedLight);
        void ChangeLightMode(ushort segmentId, ExtVehicleType vehicleType, LightMode mode);
        void SetLights(bool noTransition = false);
        void SimulationStep();
        void SkipStep(bool setLights = true, int prevStepRefIndex = -1);
        void Start();
        void Start(int step);
        void Stop();
        void OnGeometryUpdate();
        void RemoveNodeFromGroup(ushort otherNodeId);
    }
}