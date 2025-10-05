namespace TrafficManager.UI {
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.U;
    using TrafficManager.Util;
    using UnityEngine;

    public class RemoveVehicleButtonExtender : MonoBehaviour {
        private IList<UIButton> buttons;

        public void Start() {
            Log._Debug($"{GetType().Name} started.");
            buttons = new List<UIButton>();

            var citizenVehicleInfoPanel
                = GameObject.Find("(Library) CitizenVehicleWorldInfoPanel")
                            .GetComponent<CitizenVehicleWorldInfoPanel>();

            if (citizenVehicleInfoPanel != null) {
                buttons.Add(AddRemoveVehicleButton(citizenVehicleInfoPanel));
            }

            var cityServiceVehicleInfoPanel
                = GameObject.Find("(Library) CityServiceVehicleWorldInfoPanel")
                            .GetComponent<CityServiceVehicleWorldInfoPanel>();

            if (cityServiceVehicleInfoPanel != null) {
                buttons.Add(AddRemoveVehicleButton(cityServiceVehicleInfoPanel));
            }

            var publicTransportVehicleInfoPanel
                = GameObject.Find("(Library) PublicTransportVehicleWorldInfoPanel")
                            .GetComponent<PublicTransportVehicleWorldInfoPanel>();

            if (publicTransportVehicleInfoPanel != null) {
                buttons.Add(AddRemoveVehicleButton(publicTransportVehicleInfoPanel));
            }
        }

        public void OnDestroy() {
            if (buttons == null) {
                return;
            }

            foreach (UIButton button in buttons) {
                Destroy(button.gameObject);
            }
        }

        protected UIButton AddRemoveVehicleButton(WorldInfoPanel panel) {
            UIButton button = new GameObject("RemoveVehicleInstanceButton")
                .AddComponent<RemoveVehicleButton>();
            UITextField textField = panel.Find<UITextField>("VehicleName");
            if (textField && textField.size.x > 220f) {
                // resize and move VehicleName text field to fit the mod despawn button in the row
                Vector3 posOffset;
                Vector2 sizeOffset;
                if (panel is CitizenVehicleWorldInfoPanel) {
                    posOffset = new Vector3(12, 0);
                    sizeOffset = new Vector2(-28, 0);
                } else if (panel is CityServiceVehicleWorldInfoPanel) {
                    posOffset = new Vector3(20, 0);
                    sizeOffset = new Vector2(-22, 0);
                } else {
                    posOffset = new Vector3(17, 0);
                    sizeOffset = new Vector2(-28, 0);
                }

                textField.relativePosition += posOffset;
                textField.size += sizeOffset;
            }

            button.AlignTo(panel.component, UIAlignAnchor.TopLeft);
            button.relativePosition = new Vector3(45, 7f);

            Log._Debug($"Added {button} to {panel}");
            return button;
        }

        public class RemoveVehicleButton : BaseUButton {
            public override void Start() {
                base.Start();
                this.Skin = ButtonSkin.CreateSimple(
                                          foregroundPrefix: "Clear",
                                          backgroundPrefix: "Clear")
                                      .CanHover()
                                      .CanActivate();

                // This creates an atlas for a single button
                var futureAtlas = new U.AtlasBuilder(
                    atlasName: "RemoveVehButton_Atlas",
                    loadingPath: "Clear",
                    sizeHint: new IntVector2(256));
                this.Skin.UpdateAtlasBuilder(
                    atlasBuilder: futureAtlas,
                    spriteSize: new IntVector2(50));
                this.atlas = futureAtlas.CreateAtlas();

                UpdateButtonSkinAndTooltip();
                width = height = 28f;
            }

            public override void HandleClick(UIMouseEventParameter p) {
                InstanceID instance = WorldInfoPanel.GetCurrentInstanceID();
                Log._Debug($"Current vehicle instance: {instance.Vehicle}");

                if (instance.Vehicle != 0) {
                    Singleton<SimulationManager>.instance.AddAction(
                        () => Singleton<VehicleManager>.instance.ReleaseVehicle(instance.Vehicle));
                } else if (instance.ParkedVehicle != 0) {
                    Singleton<SimulationManager>.instance.AddAction(
                        () => Singleton<VehicleManager>.instance.ReleaseParkedVehicle(instance.ParkedVehicle));
                }
            }

            protected override bool IsActive() => false;

            // public override Texture2D AtlasTexture => Textures.MainMenu.RemoveButton;

            protected override string U_OverrideTooltipText() =>
                Translation.Menu.Get("Button:Remove this vehicle");

            protected override bool IsVisible() => true;

            public override bool CanActivate() {
                return false;
            }
        }
    }
}
