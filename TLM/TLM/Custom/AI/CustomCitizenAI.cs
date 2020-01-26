namespace TrafficManager.Custom.AI {
    using JetBrains.Annotations;
    using TrafficManager.API.Manager;
    using TrafficManager.Manager;
    using TrafficManager.RedirectionFramework.Attributes;
    using UnityEngine;

    [TargetType(typeof(CitizenAI))]
    public class CustomCitizenAI : CitizenAI {
        [RedirectMethod]
        [UsedImplicitly]
        public bool CustomStartPathFind(ushort instanceId,
                                        ref CitizenInstance citizenData,
                                        Vector3 startPos,
                                        Vector3 endPos,
                                        VehicleInfo vehicleInfo,
                                        bool enableTransport,
                                        bool ignoreCost) {
            IExtCitizenInstanceManager extCitizenInstanceManager = Constants.ManagerFactory.ExtCitizenInstanceManager;
            IExtCitizenManager extCitizenManager = Constants.ManagerFactory.ExtCitizenManager;
            return extCitizenInstanceManager.StartPathFind(
                instanceId,
                ref citizenData,
                ref extCitizenInstanceManager.ExtInstances[instanceId],
                ref extCitizenManager.ExtCitizens[citizenData.m_citizen],
                startPos,
                endPos,
                vehicleInfo,
                enableTransport,
                ignoreCost);
        }
    }
}
