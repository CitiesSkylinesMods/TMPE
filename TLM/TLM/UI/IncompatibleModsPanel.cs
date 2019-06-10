using System;
using System.Collections.Generic;
using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.IO;
using ColossalFramework.PlatformServices;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using CSUtil.Commons;
using UnityEngine;
using static ColossalFramework.Plugins.PluginManager;

namespace TrafficManager.UI
{
    public class IncompatibleModsPanel : UIPanel
    {
        private const ulong LOCAL_MOD = ulong.MaxValue;

        private static IncompatibleModsPanel _instance;
        private UILabel title;
        private UIButton closeButton;
        private UISprite warningIcon;
        private UIPanel mainPanel;
        private UICheckBox runModsCheckerOnStartup;
        private UIComponent blurEffect;

        /// <summary>
        /// List of incompatible mods from <see cref="TrafficManager.Util.ModsCompatibilityChecker"/>.
        /// </summary>
        public Dictionary<PluginInfo, string> IncompatibleMods { get; set; }

        /// <summary>
        /// Initialises the dialog, populates it with list of incompatible mods, and adds it to the modal stack.
        /// 
        /// If the modal stack was previously empty, a blur effect is added over the screen background.
        /// </summary>
        public void Initialize()
        {
            Log._Debug("IncompatibleModsPanel initialize");
            if (mainPanel != null)
            {
                mainPanel.OnDestroy();
            }

            isVisible = true;

            mainPanel = AddUIComponent<UIPanel>();
            mainPanel.backgroundSprite = "UnlockingPanel2";
            mainPanel.color = new Color32(75, 75, 135, 255);
            width = 600;
            height = 440;
            mainPanel.width = 600;
            mainPanel.height = 440;

            Vector2 resolution = UIView.GetAView().GetScreenResolution();
            relativePosition = new Vector3((resolution.x / 2) - 300, resolution.y / 3);
            mainPanel.relativePosition = Vector3.zero;

            warningIcon = mainPanel.AddUIComponent<UISprite>();
            warningIcon.size = new Vector2(40f, 40f);
            warningIcon.spriteName = "IconWarning";
            warningIcon.relativePosition = new Vector3(15, 15);
            warningIcon.zOrder = 0;

            title = mainPanel.AddUIComponent<UILabel>();
            title.autoSize = true;
            title.padding = new RectOffset(10, 10, 15, 15);
            title.relativePosition = new Vector2(60, 12);

#if LABS
            title.text = "TM:PE LABS " + Translation.GetString("Traffic_Manager_detected_incompatible_mods");
#elif DEBUG
            title.text = "TM:PE DEBUG " + Translation.GetString("Traffic_Manager_detected_incompatible_mods");
#else // STABLE
            title.text = "TM:PE STABLE " + Translation.GetString("Traffic_Manager_detected_incompatible_mods");
#endif

            closeButton = mainPanel.AddUIComponent<UIButton>();
            closeButton.eventClick += CloseButtonClick;
            closeButton.relativePosition = new Vector3(width - closeButton.width - 45, 15f);
            closeButton.normalBgSprite = "buttonclose";
            closeButton.hoveredBgSprite = "buttonclosehover";
            closeButton.pressedBgSprite = "buttonclosepressed";

            UIPanel panel = mainPanel.AddUIComponent<UIPanel>();
            panel.relativePosition = new Vector2(20, 70);
            panel.size = new Vector2(565, 320);

            UIHelper helper = new UIHelper(mainPanel);
            runModsCheckerOnStartup = helper.AddCheckbox(Translation.GetString("Scan_for_known_incompatible_mods_on_startup"), State.GlobalConfig.Instance.Main.ScanForKnownIncompatibleModsAtStartup, RunModsCheckerOnStartup_eventCheckChanged) as UICheckBox;
            runModsCheckerOnStartup.relativePosition = new Vector3(20, height - 30f);

            UIScrollablePanel scrollablePanel = panel.AddUIComponent<UIScrollablePanel>();
            scrollablePanel.backgroundSprite = string.Empty;
            scrollablePanel.size = new Vector2(550, 340);
            scrollablePanel.relativePosition = new Vector3(0, 0);
            scrollablePanel.clipChildren = true;
            scrollablePanel.autoLayoutStart = LayoutStart.TopLeft;
            scrollablePanel.autoLayoutDirection = LayoutDirection.Vertical;
            scrollablePanel.autoLayout = true;

            // Populate list of incompatible mods
            if (IncompatibleMods.Count != 0)
            {
                IncompatibleMods.ForEach((pair) =>
                {
                    CreateEntry(ref scrollablePanel, pair.Value, pair.Key);
                });
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

            // Add blur effect if applicable
            blurEffect = GameObject.Find("ModalEffect").GetComponent<UIComponent>();
            AttachUIComponent(blurEffect.gameObject);
            blurEffect.size = new Vector2(resolution.x, resolution.y);
            blurEffect.absolutePosition = new Vector3(0, 0);
            blurEffect.SendToBack();
            if (blurEffect != null)
            {
                blurEffect.isVisible = true;
                ValueAnimator.Animate("ModalEffect", delegate(float val) { blurEffect.opacity = val; }, new AnimatedFloat(0f, 1f, 0.7f, EasingType.CubicEaseOut));
            }

            // Make sure modal dialog is in front of all other UI
            BringToFront();
        }

        /// <summary>
        /// Allows the user to press "Esc" to close the dialog.
        /// </summary>
        /// 
        /// <param name="p">Details about the key press.</param>
        protected override void OnKeyDown(UIKeyEventParameter p)
        {
            if (Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Return))
            {
                TryPopModal();
                p.Use();
                Hide();
                Unfocus();
            }

            base.OnKeyDown(p);
        }

        /// <summary>
        /// Hnadles click of the "Run incompatible check on startup" checkbox and updates game options accordingly.
        /// </summary>
        /// 
        /// <param name="value">The new value of the checkbox; <c>true</c> if checked, otherwise <c>false</c>.</param>
        private void RunModsCheckerOnStartup_eventCheckChanged(bool value)
        {
            Log._Debug("Incompatible mods checker run on game launch changed to " + value);
            State.Options.setScanForKnownIncompatibleMods(value);
        }

        /// <summary>
        /// Handles click of the "close dialog" button; pops the dialog off the modal stack.
        /// </summary>
        /// 
        /// <param name="component">Handle to the close button UI component (not used).</param>
        /// <param name="eventparam">Details about the click event.</param>
        private void CloseButtonClick(UIComponent component, UIMouseEventParameter eventparam)
        {
            CloseDialog();
            eventparam.Use();
        }

        /// <summary>
        /// Pops the popup dialog off the modal stack.
        /// </summary>
        private void CloseDialog()
        {
            closeButton.eventClick -= CloseButtonClick;
            TryPopModal();
            Hide();
            Unfocus();
        }

        /// <summary>
        /// Creates a panel representing the mod and adds it to the <paramref name="parent"/> UI component.
        /// </summary>
        /// 
        /// <param name="parent">The parent UI component that the panel will be added to.</param>
        /// <param name="modName">The name of the mod, which is displayed to user.</param>
        /// <param name="mod">The <see cref="PluginInfo"/> instance of the incompatible mod.</param>
        private void CreateEntry(ref UIScrollablePanel parent, string modName, PluginInfo mod)
        {
            string caption = mod.publishedFileID.AsUInt64 == LOCAL_MOD ? Translation.GetString("Delete") : Translation.GetString("Unsubscribe");

            UIPanel panel = parent.AddUIComponent<UIPanel>();
            panel.size = new Vector2(560, 50);
            panel.backgroundSprite = "ContentManagerItemBackground";

            UILabel label = panel.AddUIComponent<UILabel>();
            label.text = modName;
            label.textAlignment = UIHorizontalAlignment.Left;
            label.relativePosition = new Vector2(10, 15);

            CreateButton(panel, caption, (int)panel.width - 170, 10, delegate (UIComponent component, UIMouseEventParameter param) { UnsubscribeClick(component, param, mod); });
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
        private void UnsubscribeClick(UIComponent component, UIMouseEventParameter eventparam, PluginInfo mod)
        {
            eventparam.Use();
            bool success = false;

            // disable button to prevent accidental clicks
            component.isEnabled = false;

            Log.Info($"Removing incompatible mod '{mod.name}' from {mod.modPath}");
            if (mod.publishedFileID.AsUInt64 == LOCAL_MOD)
            {
                success = DeleteLocalTMPE(mod);
            }
            else
            {
                success = PlatformService.workshop.Unsubscribe(mod.publishedFileID);
            }

            if (success)
            {
                IncompatibleMods.Remove(mod);
                component.parent.Disable();
                component.parent.isVisible = false;

                if (IncompatibleMods.Count == 0)
                {
                    CloseDialog();
                }
            }
            else
            {
                Log.Warning($"Failed to remove mod '{mod.name}'");
                component.isEnabled = true;
            }
        }

        /// <summary>
        /// Deletes a locally installed TM:PE mod.
        /// </summary>
        /// 
        /// <param name="mod">The <see cref="PluginInfo"/> associated with the mod that needs deleting.</param>
        /// 
        /// <returns>Returns <c>true</c> if successfully deleted, otherwise <c>false</c>.</returns>
        private bool DeleteLocalTMPE(PluginInfo mod)
        {
            try
            {
                Log._Debug($"Deleting local TM:PE from {mod.modPath}");
                //mod.Unload();
                DirectoryUtils.DeleteDirectory(mod.modPath);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates an `Unsubscribe` or `Delete` button (as applicable to mod location) and attaches it to the <paramref name="parent"/> UI component.
        /// </summary>
        /// 
        /// <param name="parent">The parent UI component which the button will be attached to.</param>
        /// <param name="text">The translated text to display on the button.</param>
        /// <param name="x">The x position of the top-left corner of the button, relative to <paramref name="parent"/>.</param>
        /// <param name="y">The y position of the top-left corner of the button, relative to <paramref name="parent"/>.</param>
        /// <param name="eventClick">The event handler for when the button is clicked.</param>
        private void CreateButton(UIComponent parent, string text, int x, int y, MouseEventHandler eventClick)
        {
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

        /// <summary>
        /// Pops the dialog from the modal stack. If no more modal dialogs are present, the background blur effect is also removed.
        /// </summary>
        private void TryPopModal()
        {
            if (UIView.HasModalInput())
            {
                UIView.PopModal();
                UIComponent component = UIView.GetModalComponent();
                if (component != null)
                {
                    UIView.SetFocus(component);
                }
            }

            if (blurEffect != null && UIView.ModalInputCount() == 0)
            {
                ValueAnimator.Animate("ModalEffect", delegate (float val) { blurEffect.opacity = val; }, new AnimatedFloat(1f, 0f, 0.7f, EasingType.CubicEaseOut), delegate () { blurEffect.Hide(); });
            }
        }
    }
}