namespace TrafficManager.Patch._CitizenAI._HumanAI.Connection {
    using System;
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

    internal class HumanAIConnection {
        internal HumanAIConnection(SpawnDelegate spawnCitizenAI,
                                   StartPathFindDelegate startPathFindCitizenAI,
                                   SimulationStepDelegate simulationStepCitizenAI,
                                   ArriveAtDestinationDelegate arriveAtDestination,
                                   InvalidPathHumanAIDelegate invalidPath,
                                   PathfindFailureHumanAIDelegate pathfindFailure,
                                   PathfindSuccessHumanAIDelegate pathfindSuccess) {
            SpawnCitizenAI = spawnCitizenAI ?? throw new ArgumentNullException(nameof(spawnCitizenAI));
            StartPathFindCitizenAI = startPathFindCitizenAI ?? throw new ArgumentNullException(nameof(startPathFindCitizenAI));
            SimulationStepCitizenAI = simulationStepCitizenAI ?? throw new ArgumentNullException(nameof(simulationStepCitizenAI));
            ArriveAtDestination = arriveAtDestination ?? throw new ArgumentNullException(nameof(arriveAtDestination));
            InvalidPath = invalidPath ?? throw new ArgumentNullException(nameof(invalidPath));
            PathfindFailure = pathfindFailure ?? throw new ArgumentNullException( nameof(pathfindFailure));
            PathfindSuccess = pathfindSuccess ?? throw new ArgumentNullException(nameof(pathfindSuccess));
        }

        public SpawnDelegate SpawnCitizenAI { get; }
        public StartPathFindDelegate StartPathFindCitizenAI { get; }
        public SimulationStepDelegate SimulationStepCitizenAI { get; }
        public ArriveAtDestinationDelegate ArriveAtDestination { get; }
        public InvalidPathHumanAIDelegate InvalidPath { get; }
        public PathfindFailureHumanAIDelegate PathfindFailure { get; }
        public PathfindSuccessHumanAIDelegate PathfindSuccess { get; }
    }
}