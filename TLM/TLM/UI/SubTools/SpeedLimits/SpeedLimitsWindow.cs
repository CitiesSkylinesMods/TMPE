namespace TrafficManager.UI.SubTools.SpeedLimits {
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using TrafficManager.U.Panel;
    using UnityEngine;

    /// <summary>Implements U window for Speed Limits palette and speed defaults.</summary>
    internal class SpeedLimitsWindow : U.Panel.BaseUWindowPanel {
        private const string GAMEOBJECT_NAME = "TMPE_SpeedLimits";

        /// <summary>Called by Unity on instantiation once when the game is running.</summary>
        public override void Start() {
            base.Start();
            UIUtil.MakeUniqueAndSetName(gameObject, GAMEOBJECT_NAME);

            // the GenericPanel sprite is silver, make it dark
            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(64, 64, 64, 240);
            this.SetOpacityFromGuiOpacity();
        }

        /// <summary>Populate the window using UIBuilder of the window panel.</summary>
        /// <param name="builder">The root builder of this window.</param>
        public void SetupControls(UiBuilder<SpeedLimitsWindow> builder) {
            //----------------------------------------
            // Begin mode buttons panel (left side)
            //----------------------------------------
            void ButtonpanelSetupFn(UPanel p) => p.name = GAMEOBJECT_NAME + "_ModesPanel";
            using (var modePanelB = builder.ChildPanel<U.Panel.UPanel>(ButtonpanelSetupFn)) {
                void ButtonpanelResizeFn(UResizer r) {
                    r.Stack(mode: UStackMode.Below);
                    r.FitToChildren();
                }

                modePanelB.ResizeFunction(ButtonpanelResizeFn);

                // Edit Segments/Lanes mode button
                using (var editmodeBtnB = modePanelB.Button<U.Button.UButton>()) {
                    editmodeBtnB.Control.tooltip = "Override speed limits for one road or segment";
                    void EditmodeResizeFn(UResizer r) {
                        r.Width(UValue.FixedSize(40f));
                        r.Height(UValue.FixedSize(40f));
                        r.Stack(UStackMode.Below);
                    }

                    editmodeBtnB.ResizeFunction(EditmodeResizeFn);
                }

                // Edit Defaults mode button
                using (var defaultsmodeBtnB = modePanelB.Button<U.Button.UButton>()) {
                    defaultsmodeBtnB.Control.tooltip = "Edit default speed limits for all roads of that type";
                    void DefaultsmodeResizeFn(UResizer r) {
                        r.Width(UValue.FixedSize(40f));
                        r.Height(UValue.FixedSize(40f));
                        r.Stack(UStackMode.Below);
                    }

                    defaultsmodeBtnB.ResizeFunction(DefaultsmodeResizeFn);
                }
            } // mode buttons panel

            //----------------------------------------
            // Begin speed palette panel (right side)
            //----------------------------------------
            void PaletteSetupFn(UPanel p) => p.name = GAMEOBJECT_NAME + "_PalettePanel";
            using (var palettePanelB = builder.ChildPanel<U.Panel.UPanel>(PaletteSetupFn)) {
                palettePanelB.SetPadding(U.UConst.UIPADDING);

                void PaletteResizeFn(UResizer r) {
                    r.Width(UValue.FixedSize(250f));
                    r.Height(UValue.FixedSize(90f));
                    r.Stack(mode: UStackMode.ToTheRight);
                    // r.FitToChildren();
                }

                palettePanelB.ResizeFunction(PaletteResizeFn);
            } // speed palette panel
        }
    }
}