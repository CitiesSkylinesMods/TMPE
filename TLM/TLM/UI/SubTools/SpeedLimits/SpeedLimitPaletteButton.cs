namespace TrafficManager.UI.SubTools.SpeedLimits {
    using System;
    using ColossalFramework.UI;
    using TrafficManager.API.Util;
    using TrafficManager.State;
    using TrafficManager.U;
    using UnityEngine;

    /// <summary>
    /// Speed limits palette button, has speed assigned to it, and on click will update the selected
    /// speed in the tool, and highlight self.
    /// The palette buttons only carry the speed value, but the intent where the value goes
    /// (override for segment of default for road type) is defined by the other window buttons.
    /// </summary>
    internal class SpeedLimitPaletteButton
        : UButton,
          IObserver<ModUI.EventPublishers.UIScaleNotification>
    {
        /// <summary>Button width if it contains value less than 100 and is not selected in the palette.</summary>
        public const float DEFAULT_WIDTH = 40f;

        /// <summary>Narrow width is used for speeds below 100.</summary>
        public const float DEFAULT_WIDTH_NARROW = 30f;

        /// <summary>Width when the button is active (a bit larger to fit the larger text).</summary>
        public const float SELECTED_WIDTH = 60f;

        /// <summary>Button height.</summary>
        public const float DEFAULT_HEIGHT = 60f;

        /// <summary>Button must know its speed value, zero is reset, 1000km/h is unlimited.</summary>
        public SetSpeedLimitAction AssignedAction;

        public SpeedLimitsTool ParentTool;

        /// <summary>Label below the speed limit button displaying alternate unit.</summary>
        public ULabel AltUnitsLabel;

        private IDisposable uiScaleChangeUnsubscriber_ = null;

        public override void Awake() {
            if (this.uiScaleChangeUnsubscriber_ == null) {
                this.uiScaleChangeUnsubscriber_ = ModUI.Instance.Events.UiScale.Subscribe(this);
            }
            base.Awake();
        }

        protected override void OnClick(UIMouseEventParameter p) {
            base.OnClick(p);

            // Tell the parent to update all buttons this will unmark all inactive buttons and
            // mark one which is active. The call turns back here to this.UpdateSpeedlimitButton()
            this.ParentTool.OnPaletteButtonClicked(this.AssignedAction);
        }

        private bool IsResetToDefault() {
            return this.AssignedAction.Type == SetSpeedLimitAction.ActionType.ResetToDefault;
        }

        private bool IsUnlimited() {
            return this.AssignedAction.Type == SetSpeedLimitAction.ActionType.Unlimited;
        }

        /// <summary>If button active state changes, update visual to highlight it.</summary>
        public void UpdateSpeedlimitButton() {
            if (this.IsActive()) {
                this.UpdateSpeedlimitButton_Active();
            } else {
                this.UpdateSpeedlimitButton_Inactive();
            }
        }

        /// <summary>Active button has large text and is blue.</summary>
        public void UpdateSpeedlimitButton_Active() {
            // Special values (reset and default do not become larger)
            if (this.IsResetToDefault()) {
                this.ColorizeAllStates(Color.red); // Red for special buttons, when active
                this.textScale = 2.0f * UIScaler.UIScale; // Bigger
            } else {
                // Blue for speed buttons, when active
                this.ColorizeAllStates(new Color32(0, 128, 255, 255));
                if (this.IsUnlimited()) {
                    this.textScale = 2.8f * UIScaler.UIScale; // Even bigger than X
                } else {
                    this.textScale = 2.0f * UIScaler.UIScale;
                }
            }

            // Can't set width directly, but can via the resizer
            var w = this.text.Length <= 2 ? DEFAULT_WIDTH_NARROW : DEFAULT_WIDTH;
            this.GetResizerConfig().FixedSize.x = w * 1.5f;


            if (this.AltUnitsLabel) {
                this.AltUnitsLabel.Show();
            }
        }

        /// <summary>Inactive button has normal-size text and is silver-gray.</summary>
        public void UpdateSpeedlimitButton_Inactive() {
            // Special values (reset and default do not become larger)
            if (this.IsResetToDefault()) {
                this.textScale = 1.6f * UIScaler.UIScale; // Bigger
            } else {
                if (this.IsUnlimited()) {
                    this.textScale = 2.4f * UIScaler.UIScale; // Even bigger than X
                } else {
                    this.textScale = UIScaler.UIScale;
                }

                // Can't set width directly, but can via the resizer
                var w = this.text.Length <= 2 ? DEFAULT_WIDTH_NARROW : DEFAULT_WIDTH;
                this.GetResizerConfig().FixedSize.x = w;
            }

            this.ColorizeAllStates(new Color32(128, 128, 128, 255));

            if (this.AltUnitsLabel) {
                this.AltUnitsLabel.Hide();
            }
        }

        public override bool CanActivate() => true;

        protected override bool IsActive() {
            return this.AssignedAction.NearlyEqual(this.ParentTool.SelectedAction);
        }

        public void OnUpdate(ModUI.EventPublishers.UIScaleNotification subject) {
            this.UpdateSpeedlimitButton();
            this.Invalidate();
        }

        public override void OnDestroy() {
            if (this.uiScaleChangeUnsubscriber_ != null) {
                this.uiScaleChangeUnsubscriber_.Dispose();
            }
            base.OnDestroy();
        }
    }
}