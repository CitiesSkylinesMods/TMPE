namespace TrafficManager.Patch._CitizenAI._ResidentAI.Connection {
    using System;

    public delegate int GetTaxiProbabilityResidentDelegate(ResidentAI instance,
                                                           ushort instanceID,
                                                           ref CitizenInstance citizenData,
                                                           Citizen.AgeGroup ageGroup);

    public delegate int GetBikeProbabilityResidentDelegate(ResidentAI instance,
                                                           ushort instanceID,
                                                           ref CitizenInstance citizenData,
                                                           Citizen.AgeGroup ageGroup);

    public delegate int GetCarProbabilityResidentDelegate(ResidentAI instance,
                                                          ushort instanceID,
                                                          ref CitizenInstance citizenData,
                                                          Citizen.AgeGroup ageGroup);

    public delegate int GetElectricCarProbabilityResidentDelegate(ResidentAI instance,
                                                                  ushort instanceID,
                                                                  ref CitizenInstance citizenData,
                                                                  Citizen.AgePhase agePhase);

    internal class ResidentAIConnection {
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