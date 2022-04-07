using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.State {
    internal static class Persistence {

        public static List<IPersistentObject> PersistentObjects { get; } = new List<IPersistentObject>();
    }
}
