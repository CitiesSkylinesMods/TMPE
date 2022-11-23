#if DEBUG
namespace TrafficManager.UI.DebugSwitches {
    using ColossalFramework;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using TrafficManager.State;
    using TrafficManager.State.ConfigData;
    using TrafficManager.UI.Helpers;

    internal class DebugSwitchCheckboxOption : CheckboxOption {
        internal DebugSwitch DebugSwitch;
        public DebugSwitchCheckboxOption(DebugSwitch sw)
            : base(sw.ToString(), Options.PersistTo.Global) {
            DebugSwitch = sw;
            Translator = val => val;
            Handler = val => DebugSwitch.SetFlags(DebugSwitch, val);
            Label = sw.ToString();
            Refresh();
        }

        public void Refresh() => Value = DebugSwitch.Get();
    }
}
#endif