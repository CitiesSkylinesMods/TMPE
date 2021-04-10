namespace TrafficManager.Patch._VehicleAI._PassengerCarAI {
    using System.Reflection;
    using ColossalFramework.Globalization;
    using Connection;
    using HarmonyLib;
    using JetBrains.Annotations;
    using Manager.Impl;
    using State;
    using Util;

    [UsedImplicitly]
    [HarmonyPatch]
    public class GetLocalizedStatusPatch {

        private delegate string TargetDelegate(ushort vehicleID, ref Vehicle data, out InstanceID target);

        [UsedImplicitly]
        public static MethodBase TargetMethod() => TranspilerUtil.DeclaredMethod<TargetDelegate>(typeof(PassengerCarAI), nameof(PassengerCarAI.GetLocalizedStatus));

        private static GetDriverInstanceDelegate GetDriverInstance;

        [UsedImplicitly]
        public static void Prepare() {
            GetDriverInstance = GameConnectionManager.Instance.PassengerCarAIConnection.GetDriverInstance;
        }

        [UsedImplicitly]
        public static bool Prefix(ref string __result,
                                  PassengerCarAI __instance,
                                  ushort vehicleID,
                                  ref Vehicle data,
                                  out InstanceID target) {
            CitizenManager citizenManager = CitizenManager.instance;
            ushort driverInstanceId = GetDriverInstance(__instance, vehicleID, ref data);
            ushort targetBuildingId = 0;
            bool targetIsNode = false;

            if (driverInstanceId != 0) {
                if ((data.m_flags & Vehicle.Flags.Parking) != 0) {
                    uint citizen = citizenManager.m_instances.m_buffer[driverInstanceId].m_citizen;
                    if (citizen != 0u
                        && citizenManager.m_citizens.m_buffer[citizen].m_parkedVehicle != 0) {
                        target = InstanceID.Empty;
                        __result = Locale.Get("VEHICLE_STATUS_PARKING");
                        return false;
                    }
                }

                targetBuildingId = citizenManager.m_instances.m_buffer[driverInstanceId]
                                                 .m_targetBuilding;
                targetIsNode = (citizenManager.m_instances.m_buffer[driverInstanceId].m_flags
                                & CitizenInstance.Flags.TargetIsNode) != CitizenInstance.Flags.None;
            }

            if (targetBuildingId == 0) {
                target = InstanceID.Empty;
                __result = Locale.Get("VEHICLE_STATUS_CONFUSED");
                return false;
            }

            string ret;
            bool leavingCity =
                (BuildingManager.instance.m_buildings.m_buffer[targetBuildingId].m_flags
                 & Building.Flags.IncomingOutgoing) != Building.Flags.None;
            if (leavingCity) {
                target = InstanceID.Empty;
                ret = Locale.Get("VEHICLE_STATUS_LEAVING");
            } else {
                target = InstanceID.Empty;
                if (targetIsNode) {
                    target.NetNode = targetBuildingId;
                } else {
                    target.Building = targetBuildingId;
                }

                ret = Locale.Get("VEHICLE_STATUS_GOINGTO");
            }

            // NON-STOCK CODE START
            if (Options.parkingAI) {
                ret = AdvancedParkingManager.Instance.EnrichLocalizedCarStatus(
                    ret,
                    ref ExtCitizenInstanceManager.Instance.ExtInstances[driverInstanceId]);
            }

            // NON-STOCK CODE END
            __result = ret;
            return false;
        }
    }
}