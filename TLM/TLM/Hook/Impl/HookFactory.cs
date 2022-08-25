using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Hook;

namespace TrafficManager.Hook.Impl {
    public class HookFactory : IHookFactory {

        public static IHookFactory Instance = new HookFactory();

        public IJunctionRestrictionsHook JunctionRestrictionsHook => Manager.Impl.JunctionRestrictionsManager.Instance;
    }
}
