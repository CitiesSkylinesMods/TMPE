    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;

namespace TrafficManager.UI.Helpers {
    public class GuideHandler {
        private Dictionary<string, GuideWrapper> GuideTable = new Dictionary<string, GuideWrapper>();

        private GuideWrapper AddGuide(string localeKey) =>
            GuideTable[localeKey] = new GuideWrapper(Translation.GUIDE_KEY_PREFIX + localeKey);

        public GuideHandler() {
            foreach (string localeKey in LoadingExtension.TranslationDatabase.GetGuides()) {
                Log._Debug($"calling AddGuide(localeKey={localeKey}) ...");
                AddGuide(localeKey);
            }
        }

        public void Activate(string localeKey) {
            if (!GuideTable.TryGetValue(localeKey, out GuideWrapper guide)) {
                Log.Error($"Guide {localeKey} does not exists");
                LoadingExtension.TranslationDatabase.AddMissingGuideString(localeKey);
                guide = AddGuide(localeKey);
            }
            if (guide == null) {
                Log.Error("guide is null");
            } else {
                Singleton<SimulationManager>.instance.AddAction(delegate () {
                    guide.Activate();
                });
            }
        }

        public void Deactivate(string localeKey) {
            if (GuideTable.TryGetValue(localeKey, out GuideWrapper guide)) {
                if (guide == null) {
                    Log.Error("Unreachable code.");
                } else {
                    Singleton<SimulationManager>.instance.AddAction(delegate () {
                        guide.Activate();
                    });
                }
            }
        }

        public void DeactivateAll() {
            Singleton<SimulationManager>.instance.AddAction(delegate () {
                foreach (var item in GuideTable) {
                    item.Value?.Deactivate();
                } // end foreach
            }); // end AddAction()
        }
    }
}
