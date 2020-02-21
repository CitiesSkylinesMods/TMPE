namespace TrafficManager.Compatibility.GUI {
    using ColossalFramework;
    using ColossalFramework.IO;
    using ColossalFramework.PlatformServices;
    using static ColossalFramework.Plugins.PluginManager;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using TrafficManager.Compatibility.Struct;
    using TrafficManager.State;
    using TrafficManager.UI;
    using UnityEngine;

    /// <summary>
    /// I literally hate CO UI. It's not just designed by someone who merely didn't like UI,
    /// it's designed by someone who wanted to punish anyone who does like UI. It is a form
    /// of unwelcome and non-consensual sadism that must be purged from the universe.
    /// </summary>
    public class ProcessIncompatibleMods : UIPanel {
        private UILabel title_;
        private UIButton closeButton_;
        private UISprite warningIcon_;
        private UIPanel mainPanel_;
        private UICheckBox runModsCheckerOnStartup_;
        private UIComponent blurEffect_;

        /// <summary>
        /// Gets or sets list of incompatible mods.
        /// </summary>
        public Dictionary<PluginInfo, ModDescriptor> Issues { get; set; }

        /// <summary>
        /// Initialises the dialog, populates it with list of incompatible mods, and adds it to the modal stack.
        /// If the modal stack was previously empty, a blur effect is added over the screen background.
        /// </summary>
        public void Initialize() {
            Log._Debug("IncompatibleModsPanel initialize");
            if (mainPanel_ != null) {
                mainPanel_.OnDestroy();
            }

            isVisible = true;

            mainPanel_ = AddUIComponent<UIPanel>();
            mainPanel_.backgroundSprite = "UnlockingPanel2";
            mainPanel_.color = new Color32(75, 75, 135, 255);
            width = 600;
            height = 440;
            mainPanel_.width = 600;
            mainPanel_.height = 440;

            Vector2 resolution = UIView.GetAView().GetScreenResolution();
            relativePosition = new Vector3((resolution.x / 2) - 300, resolution.y / 3);
            mainPanel_.relativePosition = Vector3.zero;

            warningIcon_ = AddWarningIcon(mainPanel_);

            title_ = AddTitle(
                mainPanel_,
                TrafficManagerMod.ModName + " " +
                Translation.ModConflicts.Get("Window.Title:Detected incompatible mods"));

            closeButton_ = AddCloseButton(mainPanel_, CloseButtonClick);

            UIPanel panel = mainPanel_.AddUIComponent<UIPanel>();
            panel.relativePosition = new Vector2(20, 70);
            panel.size = new Vector2(565, 320);

            UIHelper helper = new UIHelper(mainPanel_);
            string checkboxLabel = Translation.ModConflicts.Get("Checkbox:Scan for known incompatible mods on startup");
            runModsCheckerOnStartup_ = helper.AddCheckbox(
                                          checkboxLabel,
                                          GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup,
                                          RunModsCheckerOnStartup_eventCheckChanged) as UICheckBox;
            runModsCheckerOnStartup_.relativePosition = new Vector3(20, height - 30f);

            UIScrollablePanel scrollablePanel = panel.AddUIComponent<UIScrollablePanel>();
            scrollablePanel.backgroundSprite = string.Empty;
            scrollablePanel.size = new Vector2(550, 340);
            scrollablePanel.relativePosition = new Vector3(0, 0);
            scrollablePanel.clipChildren = true;
            scrollablePanel.autoLayoutStart = LayoutStart.TopLeft;
            scrollablePanel.autoLayoutDirection = LayoutDirection.Vertical;
            scrollablePanel.autoLayout = true;

            // Populate list of incompatible mods
            if (IncompatibleMods.Count != 0) {
                IncompatibleMods.ForEach(
                    pair => { CreateEntry(ref scrollablePanel, pair.Value, pair.Key); });
            }

            scrollablePanel.FitTo(panel);
            scrollablePanel.scrollWheelDirection = UIOrientation.Vertical;
            scrollablePanel.builtinKeyNavigation = true;

            UIScrollbar verticalScroll = panel.AddUIComponent<UIScrollbar>();
            verticalScroll.stepSize = 1;
            verticalScroll.relativePosition = new Vector2(panel.width - 15, 0);
            verticalScroll.orientation = UIOrientation.Vertical;
            verticalScroll.size = new Vector2(20, 320);
            verticalScroll.incrementAmount = 25;
            verticalScroll.scrollEasingType = EasingType.BackEaseOut;

            scrollablePanel.verticalScrollbar = verticalScroll;

            UISlicedSprite track = verticalScroll.AddUIComponent<UISlicedSprite>();
            track.spriteName = "ScrollbarTrack";
            track.relativePosition = Vector3.zero;
            track.size = new Vector2(16, 320);

            verticalScroll.trackObject = track;

            UISlicedSprite thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.spriteName = "ScrollbarThumb";
            thumb.autoSize = true;
            thumb.relativePosition = Vector3.zero;
            verticalScroll.thumbObject = thumb;

            blurEffect_ = AddBlurEffect(resolution);

            BringToFront();
        }

        private UISprite AddWarningIcon(UIPanel panel) {
            UISprite sprite = panel.AddUIComponent<UISprite>();

            sprite.spriteName = "IconWarning";
            sprite.size = new Vector2(40f, 40f);
            sprite.relativePosition = new Vector3(15, 15);
            sprite.zOrder = 0;

            return sprite;
        }

        private UILabel AddTitle(UIPanel panel, string titleStr) {
            UILabel label = panel.AddUIComponent<UILabel>();

            label.autoSize = true;
            label.padding = new RectOffset(10, 10, 15, 15);
            label.relativePosition = new Vector2(60, 12);
            label.text = titleStr;

            return label;
        }

        private UIButton AddCloseButton(UIPanel panel, MouseEventHandler onClick) {
            UIButton btn = panel.AddUIComponent<UIButton>();

            btn.eventClick += onClick;
            btn.relativePosition = new Vector3(width - btn.width - 45, 15f);
            btn.normalBgSprite = "buttonclose";
            btn.hoveredBgSprite = "buttonclosehover";
            btn.pressedBgSprite = "buttonclosepressed";

            return btn;
        }

        private UIComponent AddBlurEffect(Vector2 resolution) {
            UIComponent blur = GameObject.Find("ModalEffect").GetComponent<UIComponent>();

            AttachUIComponent(blur.gameObject);
            blur.size = resolution;
            blur.absolutePosition = Vector3.zero;
            blur.SendToBack();
            blur.eventPositionChanged += OnBlurEffectPositionChange;
            blur.eventZOrderChanged += OnBlurEffectZOrderChange;
            blur.opacity = 0;
            blur.isVisible = true;

            ValueAnimator.Animate(
                "ModalEffect",
                val => blur.opacity = val,
                new AnimatedFloat(0f, 1f, 0.7f, EasingType.CubicEaseOut));

            return blur;
        }

        private void OnBlurEffectPositionChange(UIComponent component, Vector2 position) {
            component.absolutePosition = Vector3.zero;
        }

        private void OnBlurEffectZOrderChange(UIComponent component, int value) {
            component.SendToBack();
        }

        /// <summary>
        /// Allows the user to press "Esc" to close the dialog.
        /// </summary>
        ///
        /// <param name="eventparam">Details about the key press.</param>
        protected override void OnKeyDown(UIKeyEventParameter eventparam) {
            if (Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Return)) {
                eventparam.Use();
                CloseDialog();
            }

            base.OnKeyDown(eventparam);
        }

        /// <summary>
        /// Hnadles click of the "Run incompatible check on startup" checkbox and updates game options accordingly.
        /// </summary>
        ///
        /// <param name="value">The new value of the checkbox; <c>true</c> if checked, otherwise <c>false</c>.</param>
        private void RunModsCheckerOnStartup_eventCheckChanged(bool value) {
            Log._Debug("Incompatible mods checker run on game launch changed to " + value);
            OptionsGeneralTab.SetScanForKnownIncompatibleMods(value);
        }

        /// <summary>
        /// Handles click of the "close dialog" button; pops the dialog off the modal stack.
        /// </summary>
        ///
        /// <param name="component">Handle to the close button UI component (not used).</param>
        /// <param name="eventparam">Details about the click event.</param>
        private void CloseButtonClick(UIComponent component, UIMouseEventParameter eventparam) {
            eventparam.Use();
            CloseDialog();
        }

        /// <summary>
        /// Pops the popup dialog off the modal stack.
        /// </summary>
        private void CloseDialog() {
            // remove event listeners
            closeButton_.eventClick -= CloseButtonClick;
            blurEffect_.eventPositionChanged += OnBlurEffectPositionChange;
            blurEffect_.eventZOrderChanged += OnBlurEffectZOrderChange;

            UIView.PopModal();

            if (UIView.HasModalInput()) {
                UIComponent component = UIView.GetModalComponent();
                if (component != null) {
                    UIView.SetFocus(component);
                }
            } else {
                ValueAnimator.Animate(
                    "ModalEffect",
                    val => blurEffect_.opacity = val,
                    new AnimatedFloat(1f, 0f, 0.7f, EasingType.CubicEaseOut),
                    () => blurEffect_.Hide());
            }
            Hide();
            Unfocus();

            // should really destroy the dialog and all child components here
        }

        /// <summary>
        /// Creates a panel representing the mod and adds it to the <paramref name="parent"/> UI component.
        /// </summary>
        ///
        /// <param name="parent">The parent UI component that the panel will be added to.</param>
        /// <param name="modName">The name of the mod, which is displayed to user.</param>
        /// <param name="mod">The <see cref="PluginInfo"/> instance of the incompatible mod.</param>
        private void CreateEntry(ref UIScrollablePanel parent, string modName, PluginInfo mod) {
            string caption = mod.publishedFileID.AsUInt64 == LOCAL_MOD
                                 ? Translation.ModConflicts.Get("Button:Delete mod")
                                 : Translation.ModConflicts.Get("Button:Unsubscribe mod");

            UIPanel panel = parent.AddUIComponent<UIPanel>();
            panel.size = new Vector2(560, 50);
            panel.backgroundSprite = "ContentManagerItemBackground";

            UILabel label = panel.AddUIComponent<UILabel>();
            label.text = modName;
            label.textAlignment = UIHorizontalAlignment.Left;
            label.relativePosition = new Vector2(10, 15);

            CreateButton(
                panel,
                caption,
                (int)panel.width - 170,
                10,
                (component, param) => UnsubscribeClick(component, param, mod));
        }

        /// <summary>
        /// Handles click of "Unsubscribe" or "Delete" button; removes the associated mod and updates UI.
        ///
        /// Once all incompatible mods are removed, the dialog will be closed automatically.
        /// </summary>
        ///
        /// <param name="component">A handle to the UI button that was clicked.</param>
        /// <param name="eventparam">Details of the click event.</param>
        /// <param name="mod">The <see cref="PluginInfo"/> instance of the mod to remove.</param>
        private void UnsubscribeClick(UIComponent component,
                                      UIMouseEventParameter eventparam,
                                      PluginInfo mod) {
            eventparam.Use();
            bool success;

            // disable button to prevent accidental clicks
            component.isEnabled = false;
            Log.Info($"Removing incompatible mod '{mod.name}' from {mod.modPath}");

            success = mod.publishedFileID.AsUInt64 == LOCAL_MOD
                          ? DeleteLocalTMPE(mod)
                          : PlatformService.workshop.Unsubscribe(mod.publishedFileID);

            if (success) {
                IncompatibleMods.Remove(mod);
                component.parent.Disable();
                component.isVisible = false;

                // automatically close the dialog if no more mods to remove
                if (IncompatibleMods.Count == 0) {
                    CloseDialog();
                }
            } else {
                Log.Warning($"Failed to remove mod '{mod.name}'");
                component.isEnabled = true;
            }
        }

        /// <summary>
        /// Deletes a locally installed TM:PE mod.
        /// </summary>
        /// <param name="mod">The <see cref="PluginInfo"/> associated with the mod that needs deleting.</param>
        /// <returns>Returns <c>true</c> if successfully deleted, otherwise <c>false</c>.</returns>
        private bool DeleteLocalTMPE(PluginInfo mod) {
            try {
                Log._Debug($"Deleting local TM:PE from {mod.modPath}");
                // mod.Unload();
                DirectoryUtils.DeleteDirectory(mod.modPath);
                return true;
            }
            catch (Exception e) {
                return false;
            }
        }

        /// <summary>
        /// Creates an `Unsubscribe` or `Delete` button (as applicable to mod location) and attaches
        ///     it to the <paramref name="parent"/> UI component.
        /// </summary>
        /// <param name="parent">The parent UI component which the button will be attached to.</param>
        /// <param name="text">The translated text to display on the button.</param>
        /// <param name="x">The x position of the top-left corner of the button, relative to
        ///     <paramref name="parent"/>.</param>
        /// <param name="y">The y position of the top-left corner of the button, relative to
        ///     <paramref name="parent"/>.</param>
        /// <param name="eventClick">The event handler for when the button is clicked.</param>
        private void CreateButton(UIComponent parent,
                                  string text,
                                  int x,
                                  int y,
                                  MouseEventHandler eventClick) {
            var button = parent.AddUIComponent<UIButton>();
            button.textScale = 0.8f;
            button.width = 150f;
            button.height = 30;
            button.normalBgSprite = "ButtonMenu";
            button.disabledBgSprite = "ButtonMenuDisabled";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.focusedBgSprite = "ButtonMenu";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textColor = new Color32(255, 255, 255, 255);
            button.playAudioEvents = true;
            button.text = text;
            button.relativePosition = new Vector3(x, y);
            button.eventClick += eventClick;
        }
    }
}