namespace TrafficManager.Patch._CitizenAI._ResidentAI.Connection {
    using System;
    using CSUtil.Commons;
    using Util;

    public static class ResidentAIHook {
        internal static ResidentAIConnection GetConnection() {
            try {
                GetTaxiProbabilityResidentDelegate getTaxiProbability =
                    TranspilerUtil.CreateDelegate<GetTaxiProbabilityResidentDelegate>(
                        typeof(ResidentAI),
                        "GetTaxiProbability",
                        true);
                GetBikeProbabilityResidentDelegate getBikeProbability =
                    TranspilerUtil.CreateDelegate<GetBikeProbabilityResidentDelegate>(
                        typeof(ResidentAI),
                        "GetBikeProbability",
                        true);
                GetCarProbabilityResidentDelegate getCarProbability =
                    TranspilerUtil.CreateDelegate<GetCarProbabilityResidentDelegate>(
                        typeof(ResidentAI),
                        "GetCarProbability",
                        true);
                GetElectricCarProbabilityResidentDelegate getElectricCarProbability =
                    TranspilerUtil.CreateDelegate<GetElectricCarProbabilityResidentDelegate>(
                        typeof(ResidentAI),
                        "GetElectricCarProbability",
                        true);


                return new ResidentAIConnection(
                    getTaxiProbability,
                    getBikeProbability,
                    getCarProbability,
                    getElectricCarProbability);
            } catch (Exception e) {
                Log.Error(e.Message);
                return null;
            }
        }
    }
}