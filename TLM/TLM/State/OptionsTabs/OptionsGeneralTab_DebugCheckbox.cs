#if DEBUG
using ICities;
using System;
using System.Diagnostics.CodeAnalysis;
using TrafficManager.Lifecycle;
using TrafficManager.UI.Helpers;
using TrafficManager.Util;

namespace TrafficManager.State {

    /// <summary>DEBUG-only group for testing checkbox options.</summary>
    [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1516:Elements should be separated by blank line", Justification = "Brevity.")]
    [SuppressMessage("StyleCop.CSharp.LayoutRules", "SA1500:Braces for multi-line statements should not share line", Justification = "Brevity.")]
    public static class OptionsGeneralTab_DebugCheckbox {
        public static CheckboxOption DebugCheckboxA =
            new (nameof(Options.debugCheckboxA), Options.PersistTo.None) {
                Label = "Checkbox A: requires Checkbox B",
            };
        public static CheckboxOption DebugCheckboxB =
            new (nameof(Options.debugCheckboxB), Options.PersistTo.None) {
                Label = "Checkbox B: is required by Checkbox A",
            };

        static OptionsGeneralTab_DebugCheckbox() {
            try {
                DebugCheckboxA.Requires = new () {
                    { DebugCheckboxB },
                };
            }
            catch (Exception ex) {
                ex.LogException();
            }
        }

        public static void AddUI(UIHelperBase tab) {
            if (TMPELifecycle.InGameOrEditor()) return;

            var group = tab.AddGroup("Debug CheckboxOption");

            DebugCheckboxA.AddUI(group);
            DebugCheckboxB.AddUI(group);
        }
    }
}
#endif