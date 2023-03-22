namespace TrafficManager.Patch._CitizenAI._TouristAI.Connection {
    using System;
    using UnityEngine;

    public delegate int GetTaxiProbabilityDelegate(TouristAI instance, ushort instanceID, ref CitizenInstance citizenData);
    public delegate int GetBikeProbabilityDelegate(TouristAI instance);
    public delegate int GetCarProbabilityDelegate(TouristAI instance, Vector3 position);
    public delegate int GetElectricCarProbabilityDelegate(TouristAI instance, Citizen.Wealth wealth);
    public delegate int GetCamperProbabilityDelegate(TouristAI instance, Citizen.Wealth wealth);

    internal class TouristAIConnection {
        internal TouristAIConnection(GetTaxiProbabilityDelegate getTaxiProbability,
                                      GetBikeProbabilityDelegate getBikeProbability,
                                      GetCarProbabilityDelegate getCarProbability,
                                      GetElectricCarProbabilityDelegate getElectricCarProbability,
                                      GetCamperProbabilityDelegate getCamperProbabilityDelegate) {
            GetTaxiProbability = getTaxiProbability ?? throw new ArgumentNullException(nameof(getTaxiProbability));
            GetBikeProbability = getBikeProbability ?? throw new ArgumentNullException(nameof(getBikeProbability));
            GetCarProbability = getCarProbability ?? throw new ArgumentNullException(nameof(getCarProbability));
            GetElectricCarProbability = getElectricCarProbability ?? throw new ArgumentNullException(nameof(getElectricCarProbability));
            GetCamperProbability = getCamperProbabilityDelegate ?? throw new ArgumentNullException(nameof(getCamperProbabilityDelegate));
        }

        public GetTaxiProbabilityDelegate GetTaxiProbability {get;}
        public GetBikeProbabilityDelegate GetBikeProbability {get;}
        public GetCarProbabilityDelegate GetCarProbability {get;}
        public GetElectricCarProbabilityDelegate GetElectricCarProbability { get; }
        public GetCamperProbabilityDelegate GetCamperProbability { get; }
    }
}