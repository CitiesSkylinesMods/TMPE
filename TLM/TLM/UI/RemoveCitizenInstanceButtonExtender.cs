namespace TrafficManager.UI {
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
                buttons.Add(AddRemoveCitizenInstanceButton(citizenInfoPanel, "Citizen"));
            }

            var touristInfoPanel = GameObject
                                   .Find("(Library) TouristWorldInfoPanel")
                                   .GetComponent<TouristWorldInfoPanel>();

            if (touristInfoPanel != null) {
                buttons.Add(AddRemoveCitizenInstanceButton(touristInfoPanel, "Tourist"));
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

        private UIButton AddRemoveCitizenInstanceButton(WorldInfoPanel panel, string cimInstanceType) {
            UIButton button = new GameObject($"Remove{cimInstanceType}InstanceButton")
                .AddComponent<RemoveCitizenInstanceButton>();

            button.AlignTo(panel.component, UIAlignAnchor.TopRight);
            button.relativePosition += new Vector3(-button.width - 80f, 50f);
            return button;
        }

        public class RemoveCitizenInstanceButton : BaseUButton {
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
                this.atlas = this.Skin.CreateAtlas(
                    "Clear",
                    50,
                    50,
                    256,
                    this.Skin.CreateAtlasKeyset());
                UpdateButtonImageAndTooltip();
                width = height = 30;

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
                        bool isTourist = CitizenManager.instance.m_instances.m_buffer[citizenInstanceId].Info.m_citizenAI is TouristAI;
                        Constants.ServiceFactory.SimulationService.AddAction(
                            () => Constants
                                  .ServiceFactory.CitizenService
                                  .ReleaseCitizenInstance(citizenInstanceId));
                        // InfoPanel needs to be closed manually because method responsible for hiding it testing against type Citizen instead of CitizenInstance
                        // We are not removing Citizen but only instance
                        if (isTourist) {
                            WorldInfoPanel.Hide<TouristWorldInfoPanel>();
                        } else {
                            WorldInfoPanel.Hide<CitizenWorldInfoPanel>();
                        }
                    }
                }
            }

            protected override bool IsActive() => false;

            protected override string GetTooltip() =>
                Translation.Menu.Get("Button:Remove this citizen");

            protected override bool IsVisible() => true;

            public override bool CanActivate() {
                return false;
            }
        }
    }
}
