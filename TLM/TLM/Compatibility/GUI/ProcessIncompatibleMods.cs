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
    /// Given a list of problem mods, this handles the UI to resolve those issues by
    /// unsubscribing, disabling, etc.
    /// </summary>
    public class ProcessIncompatibleMods : UIPanel {
        private UILabel title_;
        private UIButton closeButton_;
        private UISprite warningIcon_;
        private UIPanel mainPanel_;
        private UIComponent blurEffect_;
        private UIScrollablePanel scrollPanel_;

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

            /*
            UIHelper helper = new UIHelper(mainPanel_);
            string checkboxLabel = Translation.ModConflicts.Get("Checkbox:Scan for known incompatible mods on startup");
            runModsCheckerOnStartup_ = helper.AddCheckbox(
                                          checkboxLabel,
                                          GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup,
                                          RunModsCheckerOnStartup_eventCheckChanged) as UICheckBox;

            runModsCheckerOnStartup_.relativePosition = new Vector3(20, height - 30f);
            */

            scrollPanel_ = AddScrollPanel(panel);

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

        private UIScrollablePanel AddScrollPanel(UIPanel panel) {
            UIScrollablePanel scroll = panel.AddUIComponent<UIScrollablePanel>();
            scroll.backgroundSprite = string.Empty;
            scroll.size = new Vector2(550, 340);
            scroll.relativePosition = new Vector3(0, 0);
            scroll.clipChildren = true;
            scroll.autoLayoutStart = LayoutStart.TopLeft;
            scroll.autoLayoutDirection = LayoutDirection.Vertical;
            scroll.autoLayout = true;

            /*
            if (IncompatibleMods.Count != 0) {
                IncompatibleMods.ForEach(
                    pair => { CreateEntry(ref scrollablePanel, pair.Value, pair.Key); });
            }
            */

            scroll.FitTo(panel);
            scroll.scrollWheelDirection = UIOrientation.Vertical;
            scroll.builtinKeyNavigation = true;

            UIScrollbar verticalScroll = panel.AddUIComponent<UIScrollbar>();
            verticalScroll.stepSize = 1;
            verticalScroll.relativePosition = new Vector2(panel.width - 15, 0);
            verticalScroll.orientation = UIOrientation.Vertical;
            verticalScroll.size = new Vector2(20, 320);
            verticalScroll.incrementAmount = 25;
            verticalScroll.scrollEasingType = EasingType.BackEaseOut;

            scroll.verticalScrollbar = verticalScroll;

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

            return scroll;
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
            if (Input.GetKey(KeyCode.Escape)) {
                eventparam.Use();
                CloseDialog();
            } else if (Input.GetKey(KeyCode.Return)) {
                // todo: default action
            } else {
                base.OnKeyDown(eventparam);
            }
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

            Hide();
            Unfocus();

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

            // should really destroy the dialog and all child components here
        }

        /// <summary>
        /// Deletes a locally installed mod.
        /// </summary>
        /// 
        /// <param name="mod">The <see cref="PluginInfo"/> associated with the mod that needs deleting.</param>
        /// 
        /// <returns>Returns <c>true</c> if successfully deleted, otherwise <c>false</c>.</returns>
        private bool DeleteLocalMod(PluginInfo mod) {
            try {
                Log.InfoFormat("Deleting local mod from {0}", mod.modPath);
                // mod.Unload(); // this caused crash
                DirectoryUtils.DeleteDirectory(mod.modPath);
                return true;
            }
            catch (Exception e) {
                Log.InfoFormat("- Failed:\n{0}", e.ToString());
                return false;
            }
        }
    }
}