namespace TrafficManager.UI.Helpers {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public interface IValuePropagator {
        void Propagate(bool value);
        void AddPropagate(IValuePropagator item, bool value);
    }
}
