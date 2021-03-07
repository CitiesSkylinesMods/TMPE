namespace TrafficManager.API.Manager.Connections {
    public delegate int GetTaxiProbabilityDelegate(TouristAI instance);

    public delegate int GetBikeProbabilityDelegate(TouristAI instance);

    public delegate int GetCarProbabilityDelegate(TouristAI instance);

    public delegate int GetElectricCarProbabilityDelegate(TouristAI instance, Citizen.Wealth wealth);

    public delegate int GetCamperProbabilityDelegate(TouristAI instance, Citizen.Wealth wealth);

    public interface ITouristAIConnection {
        GetTaxiProbabilityDelegate GetTaxiProbability { get; }
        GetBikeProbabilityDelegate GetBikeProbability { get; }
        GetCarProbabilityDelegate GetCarProbability { get; }
        GetElectricCarProbabilityDelegate GetElectricCarProbability { get; }
        GetCamperProbabilityDelegate GetCamperProbability { get; }
    }
}