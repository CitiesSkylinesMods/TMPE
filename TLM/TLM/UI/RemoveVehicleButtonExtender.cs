namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.U;
    using TrafficManager.Util;
    using UnityEngine;

    public class RemoveVehicleButtonExtender : MonoBehaviour {
        private IList<UIButton> buttons;

        public void Start() {
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

            button.AlignTo(panel.component, UIAlignAnchor.TopRight);
            button.relativePosition += new Vector3(-button.width - 55f, 50f);

            return button;
        }

        public class RemoveVehicleButton : BaseUButton {
            public override void Start() {
                base.Start();
                this.Skin = new ButtonSkin {
                    BackgroundPrefix = "Clear",
                    Prefix = "Clear",
                    BackgroundHovered = true,
                    BackgroundActive = true,
                    ForegroundHovered = true,
                    ForegroundActive = true,
                };

                // This creates an atlas for a single button
                var futureAtlas = new U.AtlasBuilder();
                this.Skin.UpdateAtlasBuilder(
                    atlasBuilder: futureAtlas,
                    spriteSize: new IntVector2(50));
                this.atlas = futureAtlas.CreateAtlas(
                    atlasName: "RemoveVehButton_Atlas",
                    loadingPath: "Clear",
                    atlasSizeHint: new IntVector2(256));

                UpdateButtonImageAndTooltip();
                width = height = 30f;
            }

            public override void HandleClick(UIMouseEventParameter p) {
                InstanceID instance = WorldInfoPanel.GetCurrentInstanceID();
                Log._Debug($"Current vehicle instance: {instance.Vehicle}");

                if (instance.Vehicle != 0) {
                    Constants.ServiceFactory.SimulationService.AddAction(
                        () => Constants.ServiceFactory.VehicleService.ReleaseVehicle(instance.Vehicle));
                } else if (instance.ParkedVehicle != 0) {
                    Constants.ServiceFactory.SimulationService.AddAction(
                        () => Constants.ServiceFactory.VehicleService.ReleaseParkedVehicle(instance.ParkedVehicle));
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
