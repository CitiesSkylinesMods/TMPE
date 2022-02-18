namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using ICities;

    public class ActionButton {
        private string _label;
        private string _tooltip;
        private bool _readOnly;

        private UIButton _ui;

        public event OnButtonClicked OnClicked;

        public OnButtonClicked Handler {
            set => OnClicked += value;
        }

        public bool HasUI => _ui != null;

        public string Label {
            get => _label;
            set {
                _label = value;
                UpdateLabel();
            }
        }

        public string Tooltip {
            get => _tooltip;
            set {
                _tooltip = value;
                UpdateTooltip();
            }
        }

        public bool ReadOnly {
            get => _readOnly;
            set {
                _readOnly = value;
                UpdateReadOnly();
            }
        }

        public void AddUI(UIHelperBase container) {
            _ui = container.AddButton(T(_label), OnClicked) as UIButton;

            UpdateTooltip();
            UpdateReadOnly();
        }

        private void UpdateLabel() {
            if (!HasUI) return;

            _ui.text = T(_label);
        }

        private void UpdateTooltip() {
            if (!HasUI) return;

            _ui.tooltip = T(_tooltip);
        }

        private void UpdateReadOnly() {
            if (!HasUI) return;

            _ui.isInteractive = !_readOnly;
            _ui.opacity = _readOnly ? 0.3f : 1f;
        }

        private string T(string key)
            => Translation.Options.Get(key);
    }
}
