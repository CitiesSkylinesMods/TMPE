using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Persistence {
    internal enum PersistenceResult {
        Success = 0,
        Failure = 1,
        Skip = 2,
    }

    internal static class PersistenceResultExtensions {
        public static void LogMessage(this PersistenceResult result, string message) {
            switch (result) {
                case PersistenceResult.Failure:
                default:
                    Log.Warning(message);
                    break;

                case PersistenceResult.Success:
                case PersistenceResult.Skip:
                    Log.Info(message);
                    break;
            }
        }
    }
}
