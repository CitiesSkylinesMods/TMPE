﻿namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.U;
    using TrafficManager.U.Button;
    using TrafficManager.UI.MainMenu;
    using TrafficManager.UI.Textures;
    using UnityEngine;

    public class RemoveCitizenInstanceButtonExtender : MonoBehaviour {
        private IList<UIButton> buttons;

        public void Start() {
            buttons = new List<UIButton>();

            var citizenInfoPanel = GameObject
                                   .Find("(Library) CitizenWorldInfoPanel")
                                   .GetComponent<CitizenWorldInfoPanel>();

            if (citizenInfoPanel != null) {
                buttons.Add(AddRemoveCitizenInstanceButton(citizenInfoPanel));
            }

            var touristInfoPanel = GameObject
                                   .Find("(Library) TouristWorldInfoPanel")
                                   .GetComponent<TouristWorldInfoPanel>();

            if (touristInfoPanel != null) {
                buttons.Add(AddRemoveCitizenInstanceButton(touristInfoPanel));
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

        private UIButton AddRemoveCitizenInstanceButton(WorldInfoPanel panel) {
            UIButton button =
                UIView.GetAView().AddUIComponent(typeof(RemoveCitizenInstanceButton)) as
                    RemoveCitizenInstanceButton;

            button.AlignTo(panel.component, UIAlignAnchor.TopRight);
            button.relativePosition += new Vector3(-button.width - 80f, 50f);

            return button;
        }

        public class RemoveCitizenInstanceButton : BaseUButton {
            public override void Start() {
                base.Start();
                width = height = MainMenuPanel.ScaledSize.GetButtonSize();
            }

            public override void HandleClick(UIMouseEventParameter p) {
                InstanceID instance = WorldInfoPanel.GetCurrentInstanceID();
                Log._Debug($"Current citizen: {instance.Citizen}");

                if (instance.Citizen != 0) {
                    ushort citizenInstanceId = 0;
                    Constants.ServiceFactory.CitizenService.ProcessCitizen(
                        instance.Citizen,
                        (uint citId, ref Citizen cit) => {
                            citizenInstanceId = cit.m_instance;
                            return true;
                        });

                    Log._Debug(
                        $"Current citizen: {instance.Citizen} Instance: {citizenInstanceId}");
                    if (citizenInstanceId != 0) {
                        Constants.ServiceFactory.SimulationService.AddAction(
                            () => Constants
                                  .ServiceFactory.CitizenService
                                  .ReleaseCitizenInstance(citizenInstanceId));
                    }
                }
            }

            public override bool IsActive() => false;

            // public override Texture2D AtlasTexture => Textures.MainMenu.RemoveButton;

            public override string ButtonName => "RemoveCitizenInstance";

            // public override string FunctionName => "RemoveCitizenInstanceNow";

            // public override string[] FunctionNames => new[] { "RemoveCitizenInstanceNow" };

            public override string GetTooltip() =>
                Translation.Menu.Get("Button:Remove this citizen");

            public override bool IsVisible() => true;

            public override bool CanActivate() {
                return false;
            }
        }
    }
}
