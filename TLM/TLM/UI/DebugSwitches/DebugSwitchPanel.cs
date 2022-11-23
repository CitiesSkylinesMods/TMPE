#if DEBUG
namespace TrafficManager.UI.DebugSwitches {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System;
    using System.Collections.Generic;
    using TrafficManager.State.ConfigData;
    using TrafficManager.U;
    using UnityEngine;
    using TrafficManager.Util;

    public class DebugSwitchPanel : UIPanel {
        private const int _defaultWidth = 400;
        private const int _defaultHeight = 700;
        private static readonly Color32 _panelBgColor = new Color32(55, 55, 55, 255);
        private static readonly RectOffset _panelPadding = new RectOffset(10, 0, 15, 0);

        private static List<DebugSwitchCheckboxOption> options_ = new();

        private UIDragHandle _header;

        static DebugSwitchPanel() {
            foreach (DebugSwitch debugSwitch in Enum.GetValues(typeof(DebugSwitch))) {
                if (debugSwitch != DebugSwitch.None) {
                    options_.Add(new DebugSwitchCheckboxOption(debugSwitch));
                }
            }
        }

        public override void Awake() {
            base.Awake();

            isVisible = true;
            canFocus = true;
            isInteractive = true;
            anchor = UIAnchorStyle.Left | UIAnchorStyle.Top | UIAnchorStyle.Proportional;
            size = new Vector2(_defaultWidth, _defaultHeight);
            backgroundSprite = "GenericPanel";
            color = _panelBgColor;
            atlas = TextureUtil.Ingame;

            AddHeader();
            AddContent();
        }

        public static void OpenModal() {
            UIView uiView = UIView.GetAView();
            if (uiView) {

                DebugSwitchPanel panel = uiView.AddUIComponent(typeof(DebugSwitchPanel)) as DebugSwitchPanel;
                if (panel) {
                    Log.Info("Opened Allow Despawn panel!");
                    UIView.PushModal(panel);
                    panel.CenterToParent();
                    panel.BringToFront();
                }
            }
        }

        protected override void OnVisibilityChanged() {
            base.OnVisibilityChanged();
            if (isVisible) {
                foreach (var option in options_) {
                    option.Refresh();
                }
            }
        }

        private void AddContent() {
            var panel = AddUIComponent<UIPanel>();
            panel.autoLayout = false;
            panel.maximumSize = GetMaxContentSize();
            panel.relativePosition = new Vector2(5, 40);
            panel.size = new Vector2(_defaultWidth - 10, _defaultHeight - _header.height);
            panel.padding = _panelPadding;
            panel.autoLayoutDirection = LayoutDirection.Vertical;

            var group = new UIHelper(panel);

            foreach (var option in options_) {
                option.AddUI(group);
            }

            panel.autoLayout = true;
        }

        private Vector2 GetMaxContentSize() {
            var resolution = GetUIView().GetScreenResolution();
            return new Vector2(_defaultWidth, resolution.y - 580f);
        }

        private void AddHeader() {
            _header = AddUIComponent<UIDragHandle>();
            _header.size = new Vector2(_defaultWidth, 42);
            _header.relativePosition = Vector2.zero;

            var title = _header.AddUIComponent<UILabel>();
            title.textScale = 1.35f;
            title.anchor = UIAnchorStyle.Top;
            title.textAlignment = UIHorizontalAlignment.Center;
            title.eventTextChanged += (_, _) => title.CenterToParent();
            title.text = "Debug switches";
            title.MakePixelPerfect();

            var cancel = _header.AddUIComponent<UIButton>();
            cancel.normalBgSprite = "buttonclose";
            cancel.hoveredBgSprite = "buttonclosehover";
            cancel.pressedBgSprite = "buttonclosepressed";
            cancel.atlas = TextureUtil.Ingame;
            cancel.size = new Vector2(32, 32);
            cancel.relativePosition = new Vector2(_defaultWidth - 37, 4);
            cancel.eventClick += (_, _) => HandleClose();
        }

        private void HandleClose() {
            if (!gameObject) return;

            if (UIView.GetModalComponent() == this) {
                UIView.PopModal();
                UIComponent modal = UIView.GetModalComponent();
                if (modal) {
                    UIView.GetAView().BringToFront(modal);
                } else {
                    UIView.GetAView().panelsLibraryModalEffect.Hide();
                }
            }

            _header = null;
            Destroy(gameObject);
            Log.Info("Allow Despawn panel closed and destroyed.");
        }
    }
}
#endif