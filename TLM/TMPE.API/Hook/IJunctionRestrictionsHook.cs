using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.API.Traffic.Enums;

namespace TrafficManager.API.Hook {
    public interface IJunctionRestrictionsHook {

        event FlagsHookHandler GetDefaults;

        event FlagsHookHandler GetConfigurable;

        public class FlagsHookArgs {

            public JunctionRestrictionFlags Mask { get; private set; }

            public JunctionRestrictionFlags Result { get; set; }

            public FlagsHookArgs(JunctionRestrictionFlags mask, JunctionRestrictionFlags result) {
                Mask = mask;
                Result = result;
            }
        }

        public delegate void FlagsHookHandler(FlagsHookArgs args);
    }
}
