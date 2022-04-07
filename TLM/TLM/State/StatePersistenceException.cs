using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.State {
    internal class StatePersistenceException : Exception {
        public StatePersistenceException(string message)
            : base(message)
            { }
    }
}
