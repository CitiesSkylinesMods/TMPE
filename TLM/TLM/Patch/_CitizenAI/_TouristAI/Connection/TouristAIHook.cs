namespace TrafficManager.Patch._CitizenAI._TouristAI.Connection {
    using System;
    using CSUtil.Commons;
    using Util;

    public static class TouristAIHook {
        internal static TouristAIConnection GetConnection() {
            try {
                GetTaxiProbabilityDelegate getTaxiProbability =
                    TranspilerUtil.CreateDelegate<GetTaxiProbabilityDelegate>(
                        typeof(TouristAI),
                        "GetTaxiProbability",
                        true);
                GetBikeProbabilityDelegate getBikeProbability =
                    TranspilerUtil.CreateDelegate<GetBikeProbabilityDelegate>(
                        typeof(TouristAI),
                        "GetBikeProbability",
                        true);
                GetCarProbabilityDelegate getCarProbability =
                    TranspilerUtil.CreateDelegate<GetCarProbabilityDelegate>(
                        typeof(TouristAI),
                        "GetCarProbability",
                        true);
                GetElectricCarProbabilityDelegate getElectricCarProbability =
                    TranspilerUtil.CreateDelegate<GetElectricCarProbabilityDelegate>(
                        typeof(TouristAI),
                        "GetElectricCarProbability",
                        true);
                GetCamperProbabilityDelegate getCamperProbability =
                    TranspilerUtil.CreateDelegate<GetCamperProbabilityDelegate>(
                        typeof(TouristAI),
                        "GetCamperProbability",
                        true);


                return new TouristAIConnection(
                    getTaxiProbability,
                    getBikeProbability,
                    getCarProbability,
                    getElectricCarProbability,
                    getCamperProbability);
            } catch (Exception e) {
                Log.Error(e.Message);
                return null;
            }
        }
    }
}