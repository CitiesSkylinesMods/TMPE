using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.API.Hook {
    public interface IHookFactory {
        IJunctionRestrictionsHook JunctionRestrictionsHook { get; }
    }
}
