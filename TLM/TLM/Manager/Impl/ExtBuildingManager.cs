namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.State;
    using UnityEngine;

    public class ExtBuildingManager
        : AbstractCustomManager,
          IExtBuildingManager
    {
        static ExtBuildingManager() {
            Instance = new ExtBuildingManager();
        }

        private ExtBuildingManager() {
            ExtBuildings = new ExtBuilding[BuildingManager.MAX_BUILDING_COUNT];
            for (uint i = 0; i < BuildingManager.MAX_BUILDING_COUNT; ++i) {
                ExtBuildings[i] = new ExtBuilding((ushort)i);
            }
        }

        public static ExtBuildingManager Instance { get; }

        /// <summary>
        /// All additional data for buildings
        /// </summary>
        public ExtBuilding[] ExtBuildings { get; }

        public void OnBeforeSimulationStep(ushort buildingId, ref Building data) {
            // slowly decrease parking space demand / public transport demand if Parking AI is active
            if (!Options.parkingAI) {
                return;
            }

            uint frameIndex = Singleton<SimulationManager>.instance.m_currentFrameIndex >> 8;
            if ((frameIndex & 1u) == 0u) {
                RemoveDemand(ref ExtBuildings[buildingId]);
            }
        }

        private void RemoveDemand(ref ExtBuilding extBuilding) {
            RemoveParkingSpaceDemand(
                ref extBuilding,
                GlobalConfig.Instance.ParkingAI.ParkingSpaceDemandDecrement);
            RemovePublicTransportDemand(
                ref extBuilding,
                GlobalConfig.Instance.ParkingAI.PublicTransportDemandDecrement,
                true);
            RemovePublicTransportDemand(
                ref extBuilding,
                GlobalConfig.Instance.ParkingAI.PublicTransportDemandDecrement,
                false);
        }

        public bool IsValid(ushort buildingId) {
            return CheckBuildingFlags(
                buildingId,
                Building.Flags.Created | Building.Flags.Collapsed | Building.Flags.Deleted,
                Building.Flags.Created);
        }

        public void Reset(ref ExtBuilding extBuilding) {
            extBuilding.parkingSpaceDemand = 0;
            extBuilding.incomingPublicTransportDemand = 0;
            extBuilding.outgoingPublicTransportDemand = 0;
        }

        public void AddParkingSpaceDemand(ref ExtBuilding extBuilding, uint delta) {
            extBuilding.parkingSpaceDemand = (byte)Math.Min(
                100,
                (int)extBuilding.parkingSpaceDemand + delta);

            RequestColorUpdate(extBuilding.buildingId);
        }

        public void RemoveParkingSpaceDemand(ref ExtBuilding extBuilding, uint delta) {
            extBuilding.parkingSpaceDemand = (byte)Math.Max(
                0,
                (int)extBuilding.parkingSpaceDemand - delta);

            RequestColorUpdate(extBuilding.buildingId);
        }

        public void ModifyParkingSpaceDemand(ref ExtBuilding extBuilding,
                                             Vector3 parkPos,
                                             int minDelta = -10,
                                             int maxDelta = 10) {
            Vector3 buildingPos = Singleton<BuildingManager>
                                  .instance.m_buildings.m_buffer[extBuilding.buildingId].m_position;
            float distance = Mathf.Clamp(
                (parkPos - buildingPos).magnitude,
                0f,
                GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding);

            float delta =
                ((maxDelta - minDelta) *
                (distance / GlobalConfig.Instance.ParkingAI.MaxParkedCarDistanceToBuilding)) +
                minDelta;

            extBuilding.parkingSpaceDemand = (byte)Mathf.Clamp(
                extBuilding.parkingSpaceDemand + (int)Mathf.Round(delta),
                0,
                100);

            RequestColorUpdate(extBuilding.buildingId);
        }

        public void
            AddPublicTransportDemand(ref ExtBuilding extBuilding, uint delta, bool outgoing) {
            byte oldDemand = outgoing
                                 ? extBuilding.outgoingPublicTransportDemand
                                 : extBuilding.incomingPublicTransportDemand;
            byte newDemand = (byte)Math.Min(100, (int)oldDemand + delta);

            if (outgoing) {
                extBuilding.outgoingPublicTransportDemand = newDemand;
            } else {
                extBuilding.incomingPublicTransportDemand = newDemand;
            }

            RequestColorUpdate(extBuilding.buildingId);
        }

        public void RemovePublicTransportDemand(ref ExtBuilding extBuilding,
                                                uint delta,
                                                bool outgoing) {
            byte oldDemand = outgoing
                                 ? extBuilding.outgoingPublicTransportDemand
                                 : extBuilding.incomingPublicTransportDemand;
            byte newDemand = (byte)Math.Max(0, (int)oldDemand - delta);

            if (outgoing) {
                extBuilding.outgoingPublicTransportDemand = newDemand;
            } else {
                extBuilding.incomingPublicTransportDemand = newDemand;
            }

            RequestColorUpdate(extBuilding.buildingId);
        }

        private void RequestColorUpdate(ushort buildingId) {
            Singleton<BuildingManager>.instance.UpdateBuildingColors(buildingId);
        }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug("Extended building data:");

            for (var i = 0; i < ExtBuildings.Length; ++i) {
                if (!IsValid((ushort)i)) {
                    continue;
                }

                Log._Debug($"Building {i}: {ExtBuildings[i]}");
            }
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            for (var i = 0; i < ExtBuildings.Length; ++i) {
                Reset(ref ExtBuildings[i]);
            }
        }

        /// <summary>
        /// Check building flags contain at least one of the flags in <paramref name="flagMask"/>.
        /// </summary>
        /// 
        /// <param name="buildingId">The id of the building to inspect.</param>
        /// <param name="flagMask">The flags to test.</param>
        /// <param name="expectedResult">If specified, ensure only the expected flags are found.</param>
        /// 
        /// <returns>Returns <c>true</c> if the test passes, otherwise <c>false</c>.</returns>
        public bool CheckBuildingFlags(ushort buildingId,
                                       Building.Flags flagMask,
                                       Building.Flags? expectedResult = null) {

            Building.Flags result = Singleton<BuildingManager>.instance.m_buildings.m_buffer[buildingId].m_flags & flagMask;

            return expectedResult == null ? result != 0 : result == expectedResult;
        }
    }
}