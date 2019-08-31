namespace TrafficManager.UI {
    using System.Collections.Generic;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using Textures;
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

        public class RemoveCitizenInstanceButton : LinearSpriteButton {
            public override void Start() {
                base.Start();
                width = Width;
                height = Height;
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

            public override bool Active => false;

            public override Texture2D AtlasTexture => TextureResources.RemoveButtonTexture2D;

            public override string ButtonName => "RemoveCitizenInstance";

            public override string FunctionName => "RemoveCitizenInstanceNow";

            public override string[] FunctionNames => new string[] { "RemoveCitizenInstanceNow" };

            public override string Tooltip => Translation.Get("Remove_this_citizen");

            public override bool Visible => true;

            public override int Width => 30;

            public override int Height => 30;

            public override bool CanActivate() {
                return false;
            }
        }
    }
}