namespace TrafficManager.UI.Helpers {
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

    public abstract class UIOptionBase {
        /// <summary>Use as tooltip for readonly UI components.</summary>
        public delegate string TranslatorDelegate(string key);

        protected const string INGAME_ONLY_SETTING = "This setting can only be changed in-game.";

        [CanBeNull]
        protected readonly SerializableOptionBase Option;
        private TranslatorDelegate _translator;
        protected string _label;
        protected string _tooltip;
        protected bool _readOnly;

        public bool Indent { get; set; }

        Options.Scope Scope => Option.Scope;

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

        public abstract bool HasUI { get; }


        public void ResetValue() => Option.ResetValue();

        protected abstract void UpdateTooltip();

        protected abstract void UpdateReadOnly();

        protected abstract void UpdateLabel();

        /// <summary>Terse shortcut for <c>Translator(key)</c>.</summary>
        /// <param name="key">The locale key to translate.</param>
        /// <returns>Returns localised string for <paramref name="key"/>.</returns>
        protected string Translate(string key) => Translator(key);
    }

    public abstract class UIOptionBase<TUI, TComponent> : UIOptionBase
        where TUI : UIComponent
    {
        protected TUI _ui;
        public override bool HasUI => _ui != null;
        public abstract TComponent AddUI(UIHelperBase container);
    }
}