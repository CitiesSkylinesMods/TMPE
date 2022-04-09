using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Persistence {
    internal static class GlobalPersistence {

        public static List<IGlobalPersistentObject> PersistentObjects { get; } = new List<IGlobalPersistentObject>();
    }
}
