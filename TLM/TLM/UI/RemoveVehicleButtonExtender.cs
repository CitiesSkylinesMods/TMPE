namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.U;
    using TrafficManager.UI.Textures;
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
            UIButton button =
                UIView.GetAView()
                      .AddUIComponent(typeof(RemoveVehicleButton)) as RemoveVehicleButton;

            button.AlignTo(panel.component, UIAlignAnchor.TopRight);
            button.relativePosition += new Vector3(-button.width - 80f, 50f);

            return button;
        }

        public class RemoveVehicleButton : BaseUButton {
            public override void Start() {
                base.Start();
                width = Width;
                height = Height;
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

            public override bool Active => false;

            public override Texture2D AtlasTexture => Textures.MainMenu.RemoveButton;

            public override string ButtonName => "RemoveVehicle";

            public override string FunctionName => "RemoveVehicleNow";

            public override string[] FunctionNames => new[] { "RemoveVehicleNow" };

            public override string Tooltip => Translation.Menu.Get("Button:Remove this vehicle");

            public override bool Visible => true;

            public override int Width => 30;

            public override int Height => 30;

            public override bool CanActivate() {
                return false;
            }
        }
    }
}
