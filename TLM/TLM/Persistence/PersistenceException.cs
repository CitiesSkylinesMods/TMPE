using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Persistence {
    internal class PersistenceException : Exception {
        public PersistenceException(string message)
            : base(message) { }
    }
}
