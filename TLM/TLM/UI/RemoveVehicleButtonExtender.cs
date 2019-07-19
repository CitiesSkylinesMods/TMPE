using ColossalFramework;
using ColossalFramework.UI;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager.Impl;
using TrafficManager.UI.MainMenu;
using UnityEngine;

namespace TrafficManager.UI {
	public class RemoveVehicleButtonExtender : MonoBehaviour {
		private IList<UIButton> buttons;

		public void Start() {
			buttons = new List<UIButton>();

            CitizenVehicleWorldInfoPanel citizenVehicleInfoPanel = GameObject.Find("(Library) CitizenVehicleWorldInfoPanel").GetComponent<CitizenVehicleWorldInfoPanel>();
			if (citizenVehicleInfoPanel != null) {
				buttons.Add(AddRemoveVehicleButton(citizenVehicleInfoPanel));
			}

            CityServiceVehicleWorldInfoPanel cityServiceVehicleInfoPanel = GameObject.Find("(Library) CityServiceVehicleWorldInfoPanel").GetComponent<CityServiceVehicleWorldInfoPanel>();
			if (cityServiceVehicleInfoPanel != null) {
				buttons.Add(AddRemoveVehicleButton(cityServiceVehicleInfoPanel));
			}

            PublicTransportVehicleWorldInfoPanel publicTransportVehicleInfoPanel = GameObject.Find("(Library) PublicTransportVehicleWorldInfoPanel").GetComponent<PublicTransportVehicleWorldInfoPanel>();
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
			UIButton button = UIView.GetAView().AddUIComponent(typeof(RemoveVehicleButton)) as RemoveVehicleButton;
			
			button.AlignTo(panel.component, UIAlignAnchor.TopRight);
			button.relativePosition += new Vector3(- button.width - 80f, 50f);

			return button;
		}

		public class RemoveVehicleButton : LinearSpriteButton {
			public override void Start() {
				base.Start();
				width = Width;
				height = Height;
			}

			public override void HandleClick(UIMouseEventParameter p) {
				InstanceID instance = WorldInfoPanel.GetCurrentInstanceID();
				Log._Debug($"Current vehicle instance: {instance.Vehicle}");
				if (instance.Vehicle != 0) {
					Constants.ServiceFactory.SimulationService.AddAction(() => Constants.ServiceFactory.VehicleService.ReleaseVehicle(instance.Vehicle));
				} else if (instance.ParkedVehicle != 0) {
					Constants.ServiceFactory.SimulationService.AddAction(() => Constants.ServiceFactory.VehicleService.ReleaseParkedVehicle(instance.ParkedVehicle));
				}
			}

			public override bool Active {
				get {
					return false;
				}
			}

			public override Texture2D AtlasTexture {
				get {
					return TextureResources.RemoveButtonTexture2D;
				}
			}

			public override string ButtonName {
				get {
					return "RemoveVehicle";
				}
			}

			public override string FunctionName {
				get {
					return "RemoveVehicleNow";
				}
			}

			public override string[] FunctionNames {
				get {
					return new string[] { "RemoveVehicleNow" };
				}
			}

			public override string Tooltip {
				get {
					return Translation.GetString("Remove_this_vehicle");
				}
			}

			public override bool Visible {
				get {
					return true;
				}
			}

			public override int Width {
				get {
					return 30;
				}
			}

			public override int Height {
				get {
					return 30;
				}
			}

			public override bool CanActivate() {
				return false;
			}
		}
	}
}
