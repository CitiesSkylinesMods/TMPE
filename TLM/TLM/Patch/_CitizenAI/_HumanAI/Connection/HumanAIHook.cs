namespace TrafficManager.Patch._CitizenAI._HumanAI.Connection {
    using System;
    using CSUtil.Commons;
    using HarmonyLib;
    using UnityEngine;
    using Util;

    public static class HumanAIHook {
        private delegate void SimulationStepTarget(ushort instanceID,
                                                   ref CitizenInstance data,
                                                   Vector3 physicsLodRefPos);

        internal static HumanAIConnection GetConnection() {
            try {
                StartPathFindDelegate startPathFindCitizenAI =
                    TranspilerUtil.CreateDelegate<StartPathFindDelegate>(
                        typeof(CitizenAI),
                        "StartPathFind",
                        true);
                SimulationStepDelegate simulationStepCitizenAI =
                    AccessTools.MethodDelegate<SimulationStepDelegate>(
                    TranspilerUtil.DeclaredMethod<SimulationStepTarget>(typeof(CitizenAI), "SimulationStep"),
                    null,
                    false);
                ArriveAtDestinationDelegate arriveAtDestination =
                    TranspilerUtil.CreateDelegate<ArriveAtDestinationDelegate>(
                        typeof(HumanAI),
                        "ArriveAtDestination",
                        true);
                SpawnDelegate spawnCitizenAI =
                    TranspilerUtil.CreateDelegate<SpawnDelegate>(
                        typeof(HumanAI),
                        "Spawn",
                        true);
                InvalidPathHumanAIDelegate invalidPath =
                    TranspilerUtil.CreateDelegate<InvalidPathHumanAIDelegate>(
                        typeof(CitizenAI),
                        "InvalidPath",
                        true);
                PathfindFailureHumanAIDelegate pathfindFailure =
                    TranspilerUtil.CreateDelegate<PathfindFailureHumanAIDelegate>(
                        typeof(HumanAI),
                        "PathfindFailure",
                        true);
                PathfindSuccessHumanAIDelegate pathfindSuccess =
                    TranspilerUtil.CreateDelegate<PathfindSuccessHumanAIDelegate>(
                        typeof(HumanAI),
                        "PathfindSuccess",
                        true);

                return new HumanAIConnection(
                    spawnCitizenAI,
                    startPathFindCitizenAI,
                    simulationStepCitizenAI,
                    arriveAtDestination,
                    invalidPath,
                    pathfindFailure,
                    pathfindSuccess);
            } catch (Exception e) {
                Log.Error(e.Message);
                return null;
            }
        }
    }
}