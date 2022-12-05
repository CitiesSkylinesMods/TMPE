namespace TrafficManager.UI.Helpers {
    using ColossalFramework.PlatformServices;
    using System.Diagnostics;
    using TrafficManager.Lifecycle;
    using TrafficManager.State;

    public class UrlButton : OptionButtonBase {
        private string _url;

        public UrlButton() {
            OnClicked -= OpenURL;
            OnClicked += OpenURL;
        }

        public static bool SteamOverlayAvailable
            => PlatformService.platformType == PlatformType.Steam &&
               PlatformService.IsOverlayEnabled();

        public string URL {
            get => _url;
            set {
                _url = value;
                UpdateTooltip();
            }
        }

        protected override void UpdateTooltip() {
            if (!HasUI) return;

            _ui.tooltip = string.IsNullOrEmpty(_tooltip)
                ? _url
                : $"{T(_tooltip)}:\n{_url}";
        }

        private void OpenURL() {
            if (string.IsNullOrEmpty(_url)) return;

            if (TMPELifecycle.InGameOrEditor())
                SimulationManager.instance.SimulationPaused = true;

            bool useSteamOverlay =
                SteamOverlayAvailable &&
                GlobalConfig.Instance.Main.OpenUrlsInSteamOverlay;

            if (useSteamOverlay) {
                PlatformService.ActivateGameOverlayToWebPage(_url);
            } else {
                //Application.OpenURL(_url);
                Process.Start(_url);
            }
        }
    }
}
