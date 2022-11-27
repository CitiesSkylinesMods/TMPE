namespace TrafficManager.State.Helpers {
    using ColossalFramework.UI;
    using ColossalFramework;
    using CSUtil.Commons;
    using ICities;
    using System.Reflection;
    using System;
    using TrafficManager.State;
    using JetBrains.Annotations;
    using TrafficManager.Lifecycle;
    using UnityEngine;
    using TrafficManager.UI;

    public abstract class UIOptionBase {
        /// <summary>Use as tooltip for readonly UI components.</summary>
        public delegate string TranslatorDelegate(string key);

        protected const string INGAME_ONLY_SETTING = "This setting can only be changed in-game.";

        [CanBeNull]
        protected SerializableOptionBase Option { get; set; }
        private TranslatorDelegate _translator;
        protected string _label;
        protected string _tooltip;
        protected bool _readOnly;

        public bool Indent { get; set; }

        public Options.Scope Scope => Option.Scope;

        /// <summary>Returns <c>true</c> if setting can persist in current <see cref="_scope"/>.</summary>
        /// <remarks>
        /// When <c>false</c>, UI component should be <see cref="_readOnly"/>
        /// and <see cref="_tooltip"/> should be set to <see cref="INGAME_ONLY_SETTING"/>.
        /// </remarks>
        public bool IsInScope =>
            Scope.IsFlagSet(Options.Scope.Global) ||
            (Scope.IsFlagSet(Options.Scope.Savegame) && TMPELifecycle.AppMode != null) ||
            Scope == Options.Scope.None;

        public TranslatorDelegate Translator {
            get => _translator ?? Translation.Options.Get;
            set => _translator = value;
        }

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
                _readOnly = !IsInScope || value;
                UpdateReadOnly();
            }
        }

        public void ResetValue() => Option.ResetValue();

        protected abstract void UpdateTooltip();

        protected abstract void UpdateReadOnly();

        protected abstract void UpdateLabel();

        /// <summary>Terse shortcut for <c>Translator(key)</c>.</summary>
        /// <param name="key">The locale key to translate.</param>
        /// <returns>Returns localised string for <paramref name="key"/>.</returns>
        protected string Translate(string key) => Translator(key);
    }

    public abstract class UIOptionBase<TVal> : UIOptionBase {
        public SerializableOptionBase<TVal> Option {
            get => base.Option as SerializableOptionBase<TVal>;
            set => base.Option = value;
        }

        public abstract void SetValue(TVal value);

        public void SetValueSafe(TVal value) {
            SimulationManager.instance.m_ThreadingWrapper.QueueMainThread(
                () => SetValue(value));
        }

        protected void EvenVisibilityChanged(UIComponent _, bool isVisible) {
            if (isVisible) {
                SetValue(Option.Value);
            }
        }

        public void InitUI(UIComponent ui) {
            UpdateLabel();
            UpdateTooltip();
            UpdateReadOnly();
            Option.OnValueChanged += SetValue;
            ui.eventVisibilityChanged += EvenVisibilityChanged;
        }

        public void OnValueChanged(TVal value) => Option.Value = value;
        public void OnValueChanged(UIComponent c, TVal value) => Option.Value = value;
    }
}