namespace TrafficManager.API.Manager.Connections {
    using UnityEngine;

    public delegate bool StartPathFindDelegate(CitizenAI instance,
                                               ushort instanceID,
                                               ref CitizenInstance citizenData);

    public delegate void SimulationStepDelegate(CitizenAI instance,
                                                ushort instanceID,
                                                ref CitizenInstance data,
                                                Vector3 physicsLodRefPos);

    public delegate void InvalidPathHumanAIDelegate(CitizenAI instance,
                                                    ushort instanceID,
                                                    ref CitizenInstance citizenData);

    public delegate void SpawnDelegate(HumanAI instance,
                                       ushort instanceID,
                                       ref CitizenInstance dat);

    public delegate void ArriveAtDestinationDelegate(HumanAI instance,
                                                     ushort instanceID,
                                                     ref CitizenInstance citizenData,
                                                     bool success);

    public delegate void PathfindSuccessHumanAIDelegate(HumanAI instance,
                                                        ushort instanceID,
                                                        ref CitizenInstance data);

    public delegate void PathfindFailureHumanAIDelegate(HumanAI instance,
                                                        ushort instanceID,
                                                        ref CitizenInstance data);

    public interface IHumanAIConnection {
        SpawnDelegate SpawnCitizenAI { get; }
        StartPathFindDelegate StartPathFindCitizenAI { get; }
        SimulationStepDelegate SimulationStepCitizenAI { get; }
        ArriveAtDestinationDelegate ArriveAtDestination { get; }
        InvalidPathHumanAIDelegate InvalidPath { get; }
        PathfindFailureHumanAIDelegate PathfindFailure { get; }
        PathfindSuccessHumanAIDelegate PathfindSuccess { get; }
    }
}