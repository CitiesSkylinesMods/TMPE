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

        public Dictionary<PluginInfo, string> IncompatibleMods { get; set; }

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

            if (IncompatibleMods.Count != 0)
            {
                int acc = 0;
                UIPanel item;
                IncompatibleMods.ForEach((pair) =>
                {
                    item = CreateEntry(ref scrollablePanel, pair.Value, pair.Key);
                    item.relativePosition = new Vector2(0, acc);
                    item.size = new Vector2(560, 50);
                    acc += 50;
                });
                item = null;
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

            BringToFront();
        }

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

        private void RunModsCheckerOnStartup_eventCheckChanged(bool value)
        {
            Log._Debug("Incompatible mods checker run on game launch changed to " + value);
            State.Options.setScanForKnownIncompatibleMods(value);
        }

        private void CloseButtonClick(UIComponent component, UIMouseEventParameter eventparam)
        {
            closeButton.eventClick -= CloseButtonClick;
            TryPopModal();
            Hide();
            Unfocus();
            eventparam.Use();
        }

        private UIPanel CreateEntry(ref UIScrollablePanel parent, string modName, PluginInfo mod)
        {
            string caption = mod.publishedFileID.AsUInt64 == LOCAL_MOD ? "Delete" : "Unsubscribe";

            UIPanel panel = parent.AddUIComponent<UIPanel>();
            panel.size = new Vector2(560, 50);
            panel.backgroundSprite = "ContentManagerItemBackground";

            UILabel label = panel.AddUIComponent<UILabel>();
            label.text = modName;
            label.textAlignment = UIHorizontalAlignment.Left;
            label.relativePosition = new Vector2(10, 15);

            CreateButton(panel, caption, (int)panel.width - 170, 10, delegate (UIComponent component, UIMouseEventParameter param) { UnsubscribeClick(component, param, mod); });

            return panel;
        }

        private void UnsubscribeClick(UIComponent component, UIMouseEventParameter eventparam, PluginInfo mod)
        {

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
                component.isVisible = false;
            }
            else
            {
                Log.Warning($"Failed to remove mod '{mod.name}'");
                component.isEnabled = true;
            }
        }

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

        private UIButton CreateButton(UIComponent parent, string text, int x, int y, MouseEventHandler eventClick)
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

            return button;
        }

        private void OnEnable()
        {
            Log._Debug("IncompatibleModsPanel enabled");
            PlatformService.workshop.eventUGCQueryCompleted += OnQueryCompleted;
            Singleton<PluginManager>.instance.eventPluginsChanged += OnPluginsChanged;
            Singleton<PluginManager>.instance.eventPluginsStateChanged += OnPluginsChanged;
            LocaleManager.eventLocaleChanged += OnLocaleChanged;
        }

        private void OnQueryCompleted(UGCDetails result, bool ioerror)
        {
            Log._Debug("IncompatibleModsPanel.OnQueryCompleted() - " + result.result.ToString("D") + " IO error?:" + ioerror);
        }

        private void OnPluginsChanged()
        {
            Log._Debug("IncompatibleModsPanel.OnPluginsChanged() - Plugins changed");
        }

        private void OnDisable()
        {
            Log._Debug("IncompatibleModsPanel disabled");
            PlatformService.workshop.eventUGCQueryCompleted -= this.OnQueryCompleted;
            Singleton<PluginManager>.instance.eventPluginsChanged -= this.OnPluginsChanged;
            Singleton<PluginManager>.instance.eventPluginsStateChanged -= this.OnPluginsChanged;
            LocaleManager.eventLocaleChanged -= this.OnLocaleChanged;
        }

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