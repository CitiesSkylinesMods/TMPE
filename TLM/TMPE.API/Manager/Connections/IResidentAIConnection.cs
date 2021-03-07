namespace TrafficManager.API.Manager.Connections {
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


    public interface IResidentAIConnection {
        GetTaxiProbabilityResidentDelegate GetTaxiProbability { get; }
        GetBikeProbabilityResidentDelegate GetBikeProbability { get; }
        GetCarProbabilityResidentDelegate GetCarProbability { get; }
        GetElectricCarProbabilityResidentDelegate GetElectricCarProbability { get; }
    }
}