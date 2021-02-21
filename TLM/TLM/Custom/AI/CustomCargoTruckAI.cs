namespace TrafficManager.Custom.AI {
    using ColossalFramework;
    using CSUtil.Commons;
    using JetBrains.Annotations;
    using System.Runtime.CompilerServices;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;
    using TrafficManager.Custom.PathFinding;
    using TrafficManager.Manager.Impl;
    using TrafficManager.RedirectionFramework.Attributes;
    using UnityEngine;

    [TargetType(typeof(CargoTruckAI))]
    public class CustomCargoTruckAI : CarAI {
        [RedirectMethod]
        public void CustomSimulationStep(ushort vehicleId, ref Vehicle vehicleData, Vector3 physicsLodRefPos) {
            if ((vehicleData.m_flags & Vehicle.Flags.Congestion) != 0 &&
                VehicleBehaviorManager.Instance.MayDespawn(ref vehicleData)) {
                Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleId);
                return;
            }

            if ((vehicleData.m_flags & Vehicle.Flags.WaitingTarget) != 0
                && (vehicleData.m_waitCounter += 1) > 20)
            {
                RemoveOffers(vehicleId, ref vehicleData);
                vehicleData.m_flags &= ~Vehicle.Flags.WaitingTarget;
                vehicleData.m_flags |= Vehicle.Flags.GoingBack;
                vehicleData.m_waitCounter = 0;

                if (!StartPathFind(vehicleId, ref vehicleData)) {
                    vehicleData.Unspawn(vehicleId);
                }
            }

            base.SimulationStep(vehicleId, ref vehicleData, physicsLodRefPos);
        }
 
        [MethodImpl(MethodImplOptions.NoInlining)]
        [RedirectReverse]
        private void RemoveOffers(ushort vehicleId, ref Vehicle data) {
            Log._DebugOnlyError("CustomCargoTruckAI.RemoveOffers called");
        }
    }
}
