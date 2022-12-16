namespace TrafficManager.UI.MainMenu {
    using ColossalFramework.UI;
    using TrafficManager.State;
    using TrafficManager.U;
    using TrafficManager.Util;

    public class DespawnButton : BaseMenuButton {
        protected override string U_OverrideTooltipText() =>
            SavedGameOptions.Instance.disableDespawning
                ? Translation.Menu.Get("Tooltip:Enable despawning")
                : Translation.Menu.Get("Tooltip:Disable despawning");

        protected override bool IsVisible() => true;

        /// <summary>
        /// Button lights up on despawning enabled (easy mode).
        /// Button remains dark on despawning disabled (hard mode).
        /// </summary>
        protected override bool IsActive() => !SavedGameOptions.Instance.disableDespawning;

        public override void SetupButtonSkin(AtlasBuilder futureAtlas) {
            // Button background (from BackgroundPrefix) is provided by MainMenuPanel.Start
            this.Skin = ButtonSkin.CreateSimple(
                                      foregroundPrefix: "TrafficDespawning",
                                      backgroundPrefix: UConst.MAINMENU_ROUND_BUTTON_BG)
                                  .CanHover(foreground: false)
                                  .CanActivate();
            this.Skin.UpdateAtlasBuilder(
                atlasBuilder: futureAtlas,
                spriteSize: new IntVector2(50));
        }

        protected override void OnClick(UIMouseEventParameter p) {
            // Immediately unclick the tool button, but toggle the option
            ModUI.GetTrafficManagerTool()?.SetToolMode(ToolMode.None);

            // Toggle the despawning value
            GameplayTab_VehicleBehaviourGroup.DisableDespawning.Value = !SavedGameOptions.Instance.disableDespawning;

            // Update currently visible tooltip
            this.UpdateTooltip(refreshTooltip: true);
            this.ApplyButtonSkin();
            // do not call base -- base.OnClick(p);
        }

        /// <summary>
        /// Handle right click, it's filtered out from OnClick() callback
        /// </summary>
        /// <param name="p"></param>
        protected override void OnMouseDown(UIMouseEventParameter p) {
            if ((p.buttons & UIMouseButton.Right) != 0 && SavedGameOptions.Instance.disableDespawning) {
                p.Use();
                UIInput.MouseUsed();
                AllowDespawn.AllowDespawningPanel.OpenModal();
            }
        }
    }
}