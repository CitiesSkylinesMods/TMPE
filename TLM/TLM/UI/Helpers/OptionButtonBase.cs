namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using ICities;
    using System;

    public abstract class OptionButtonBase {
        protected string _label;
        protected string _tooltip;
        protected bool _readOnly;

        protected UIButton _ui;

        public event OnButtonClicked OnClicked;

        public bool HasUI => _ui != null;

        public string Label {
            get => _label ?? string.Empty;
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

        public Func<string, string> Translator { get; set; } = Translation.Options.Get;

        public void AddUI(UIHelperBase container) {
            _ui = container.AddButton(T(_label), OnClicked) as UIButton;

            UpdateTooltip();
            UpdateReadOnly();
        }

        protected virtual void UpdateLabel() {
            if (!HasUI) return;

            _ui.text = T(_label);
        }

        protected virtual void UpdateTooltip() {
            if (!HasUI) return;

            _ui.tooltip = string.IsNullOrEmpty(_tooltip)
                ? string.Empty
                : T(_tooltip);
        }

        protected virtual void UpdateReadOnly() {
            if (!HasUI) return;

            _ui.isInteractive = !_readOnly;
            _ui.opacity = _readOnly ? 0.3f : 1f;
        }

        protected virtual string T(string key) => Translator(key);
    }
}
