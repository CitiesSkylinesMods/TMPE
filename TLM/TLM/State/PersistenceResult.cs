using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.State {
    internal enum PersistenceResult {
        Success = 0,
        Failure = 1,
        Skip = 2,
    }
}
