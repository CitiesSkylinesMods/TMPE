namespace TrafficManager.UI {
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using TrafficManager.U;
    using TrafficManager.Util;
    using TrafficManager.Util.Extensions;
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
            Log._Debug($"Added {button} to {panel}");
            return button;
        }

        public class RemoveCitizenInstanceButton : BaseUButton {
            public override void Start() {
                base.Start();
                this.Skin = ButtonSkin.CreateSimple(
                                          backgroundPrefix: "Clear",
                                          foregroundPrefix: "Clear")
                                      .CanActivate()
                                      .CanHover();

                // This creates an atlas for a single button
                var atlasBuilder = new U.AtlasBuilder(
                    atlasName: "RemoveCitizenButton_Atlas",
                    loadingPath: "Clear",
                    sizeHint: new IntVector2(256));
                this.Skin.UpdateAtlasBuilder(
                    atlasBuilder: atlasBuilder,
                    spriteSize: new IntVector2(50));
                this.atlas = atlasBuilder.CreateAtlas();

                UpdateButtonSkinAndTooltip();
                width = height = 30;
            }

            public override void HandleClick(UIMouseEventParameter p) {
                InstanceID worldInfoPanelInstanceId = WorldInfoPanel.GetCurrentInstanceID();
                Log._Debug($"Current citizen: {worldInfoPanelInstanceId.Citizen}");

                if (worldInfoPanelInstanceId.Citizen != 0) {
                    CitizenManager citizenManager = Singleton<CitizenManager>.instance;
                    ushort citizenInstanceId = citizenManager.m_citizens.m_buffer[worldInfoPanelInstanceId.Citizen].m_instance;

                    Log._Debug(
                        $"Current citizen: {worldInfoPanelInstanceId.Citizen} Instance: {citizenInstanceId}");
                    if (citizenInstanceId != 0) {
                        bool isTourist = citizenManager.m_instances.m_buffer[citizenInstanceId].Info.m_citizenAI is TouristAI;
                        Singleton<SimulationManager>.instance.AddAction(
                            () => Singleton<CitizenManager>.instance.ReleaseCitizenInstance(citizenInstanceId));
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

            protected override string U_OverrideTooltipText() =>
                Translation.Menu.Get("Button:Remove this citizen");

            protected override bool IsVisible() => true;

            public override bool CanActivate() {
                return false;
            }
        }
    }
}
