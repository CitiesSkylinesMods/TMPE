namespace TrafficManager.Patch._CitizenAI._ResidentAI.Connection {
    using System;
    using API.Manager.Connections;

    internal class ResidentAIConnection : IResidentAIConnection {
        internal ResidentAIConnection(GetTaxiProbabilityResidentDelegate getTaxiProbability,
                                      GetBikeProbabilityResidentDelegate getBikeProbability,
                                      GetCarProbabilityResidentDelegate getCarProbability,
                                      GetElectricCarProbabilityResidentDelegate getElectricCarProbability) {
            GetTaxiProbability = getTaxiProbability ?? throw new ArgumentNullException(nameof(getTaxiProbability));
            GetBikeProbability = getBikeProbability ?? throw new ArgumentNullException(nameof(getBikeProbability));
            GetCarProbability = getCarProbability ?? throw new ArgumentNullException(nameof(getCarProbability));
            GetElectricCarProbability = getElectricCarProbability ?? throw new ArgumentNullException(nameof(getElectricCarProbability));
        }

        public GetTaxiProbabilityResidentDelegate GetTaxiProbability {get;}
        public GetBikeProbabilityResidentDelegate GetBikeProbability {get;}
        public GetCarProbabilityResidentDelegate GetCarProbability {get;}
        public GetElectricCarProbabilityResidentDelegate GetElectricCarProbability {get;}
    }
}