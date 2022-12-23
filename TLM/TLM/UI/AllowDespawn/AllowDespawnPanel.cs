namespace TrafficManager.UI.AllowDespawn {
    using System;
    using System.Collections.Generic;
    using API.Traffic.Enums;
    using ColossalFramework;
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using Helpers;
    using Lifecycle;
    using State;
    using TrafficManager.U;
    using UnityEngine;

    public class AllowDespawningPanel : UIPanel {
        private const int _defaultWidth = 290;
        private const int _defaultHeight = 280;

        public static CheckboxOption AllowDespawnPassengerCars =
            new ("AllowDespawnPassengerCar", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Passenger Cars",
                Handler = (bool newValue) => OnChange(ExtVehicleType.PassengerCar, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.PassengerCar)
            };

        public static CheckboxOption AllowDespawnBuses =
            new ("AllowDespawnBus", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Buses",
                Handler = (bool newValue) => OnChange(ExtVehicleType.Bus, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.Bus)
            };

        public static CheckboxOption AllowDespawnTaxis =
            new ("AllowDespawnTaxi", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Taxis",
                Handler = (bool newValue) => OnChange(ExtVehicleType.Taxi, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.Taxi)
            };

        public static CheckboxOption AllowDespawnTrams =
            new ("AllowDespawnTram", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Trams",
                Handler = (bool newValue) => OnChange(ExtVehicleType.Tram, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.Tram)
            };

        public static CheckboxOption AllowDespawnTrolleybuses =
            new ("AllowDespawnTrolleybus", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Trolleybuses",
                Handler = (bool newValue) => OnChange(ExtVehicleType.Trolleybus, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.Trolleybus)
            };

        /* TODO #1368 - not supported yet
        public static CheckboxOption AllowDespawnPassengerPlanes =
            new ("AllowDespawnPassengerPlane", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Passenger Planes",
                Handler = (bool newValue) => OnChange(ExtVehicleType.PassengerPlane, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.PassengerPlane)
            };
        */

        public static CheckboxOption AllowDespawnPassengerTrains =
            new ("AllowDespawnPassengerTrain", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Passenger Trains",
                Handler = (bool newValue) => OnChange(ExtVehicleType.PassengerTrain, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.PassengerTrain)
            };

        /* TODO #1368 - not supported yet
         public static CheckboxOption AllowDespawnCargoPlanes =
            new ("AllowDespawnCargoPlane", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Cargo Planes",
                Handler = (bool newValue) => OnChange(ExtVehicleType.CargoPlane, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.CargoPlane)
            };
        */

        public static CheckboxOption AllowDespawnCargoTrains =
            new ("AllowDespawnCargoTrain", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Cargo Trains",
                Handler = (bool newValue) => OnChange(ExtVehicleType.CargoTrain, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.CargoTrain)
            };

        public static CheckboxOption AllowDespawnCargoTrucks =
            new ("AllowDespawnPassengerTruck", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Cargo Trucks",
                Handler = (bool newValue) => OnChange(ExtVehicleType.CargoTruck, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.CargoTruck)
            };

        public static CheckboxOption AllowDespawnService =
            new ("AllowDespawnService", Scope.Global) {
                Label = "AllowDespawn.Checkbox:Service vehicles",
                Handler = (bool newValue) => OnChange(ExtVehicleType.Service, newValue),
                Value = IsDespawnAllowed(ExtVehicleType.Service)
            };

        private static readonly Dictionary<CheckboxOption, ExtVehicleType> AllowDespawns = new ()
        {
            { AllowDespawnPassengerCars, ExtVehicleType.PassengerCar },
            { AllowDespawnBuses, ExtVehicleType.Bus },
            { AllowDespawnTaxis, ExtVehicleType.Taxi },
            { AllowDespawnTrams, ExtVehicleType.Tram },
            { AllowDespawnTrolleybuses, ExtVehicleType.Trolleybus },
            /*TODO #1368 - not supported yet
            { AllowDespawnPassengerPlanes, ExtVehicleType.PassengerPlane },
            */
            { AllowDespawnPassengerTrains, ExtVehicleType.PassengerTrain },
            /*TODO #1368 - not supported yet
             { AllowDespawnCargoPlanes, ExtVehicleType.CargoPlane },
             */
            { AllowDespawnCargoTrains, ExtVehicleType.CargoTrain },
            { AllowDespawnCargoTrucks, ExtVehicleType.CargoTruck },
            { AllowDespawnService, ExtVehicleType.Service },
        };

        private readonly Color32 _panelBgColor = new Color32(55, 55, 55, 255);

        private readonly RectOffset _panelPadding = new RectOffset(10, 0, 15, 0);
        private UIDragHandle _header;

        public override void Awake() {
            base.Awake();
            SubscribeToGlobalConfigChanges();

            isVisible = true;
            canFocus = true;
            isInteractive = true;
            anchor = UIAnchorStyle.Left | UIAnchorStyle.Top | UIAnchorStyle.Proportional;
            size = new Vector2(_defaultWidth, _defaultHeight);
            backgroundSprite = "GenericPanel";
            color = _panelBgColor;
            atlas = TextureUtil.Ingame;

            AddHeader();
            AddContent();
        }

        public static void OpenModal() {
            UIView uiView = UIView.GetAView();
            if (uiView) {

                AllowDespawningPanel panel = uiView.AddUIComponent(typeof(AllowDespawningPanel)) as AllowDespawningPanel;
                if (panel) {
                    Log.Info("Opened Allow Despawn panel!");
                    UIView.PushModal(panel);
                    panel.CenterToParent();
                    panel.BringToFront();
                }
            }
        }

        private void SubscribeToGlobalConfigChanges() {
            try {
                // attach observer and update checkboxes values callback
                GlobalConfigObserver configObserver = gameObject.AddComponent<GlobalConfigObserver>();
                configObserver.OnUpdateObservers += (config) => OnUpdateCheckboxes(config);
            } catch (Exception e) {
                Log.Error($"Error initializing GlobalConfigObserver {e}");
            }
        }

        private static bool IsDespawnAllowed(ExtVehicleType evt) =>
            GlobalConfig.Instance.Gameplay.AllowedDespawnVehicleTypes.IsFlagSet(evt);

        /// <summary>
        /// Handles update of GlobalConfig in case of reload or reset
        /// </summary>
        /// <param name="config">New config instance after reload</param>
        private static void OnUpdateCheckboxes(GlobalConfig config) {
            Log._Debug("Updating Allow Despawn checkboxes with updated global config value");
            foreach (KeyValuePair<CheckboxOption,ExtVehicleType> entry in AllowDespawns) {
                entry.Key.Value = IsDespawnAllowed(entry.Value);
            }
        }

        private static void OnChange(ExtVehicleType vehicleType, bool newVal) {
            if (newVal) {
                GlobalConfig.Instance.Gameplay.AllowedDespawnVehicleTypes |= vehicleType;
            } else {
                GlobalConfig.Instance.Gameplay.AllowedDespawnVehicleTypes &= ~vehicleType;
            }
            GlobalConfig.WriteConfig();
            Log._Debug($"Updated GlobalConfig AllowedDespawnVehicleTypes: {GlobalConfig.Instance.Gameplay.AllowedDespawnVehicleTypes}");
        }

        private void AddContent() {
            var panel = AddUIComponent<UIPanel>();
            panel.autoLayout = false;
            panel.maximumSize = GetMaxContentSize();
            panel.relativePosition = new Vector2(5, 40);
            panel.size = new Vector2(_defaultWidth - 10, _defaultHeight - _header.height);
            panel.padding = _panelPadding;
            panel.autoLayoutDirection = LayoutDirection.Vertical;

            var group = new UIHelper(panel);

            foreach (var allowed in AllowDespawns) {
                allowed.Key.AddUI(group);
            }

            panel.autoLayout = true;
        }

        private Vector2 GetMaxContentSize() {
            var resolution = GetUIView().GetScreenResolution();
            return new Vector2(_defaultWidth, resolution.y - 580f);
        }

        private void AddHeader() {
            _header = AddUIComponent<UIDragHandle>();
            _header.size = new Vector2(_defaultWidth, 42);
            _header.relativePosition = Vector2.zero;

            var title = _header.AddUIComponent<UILabel>();
            title.textScale = 1.35f;
            title.anchor = UIAnchorStyle.Top;
            title.textAlignment = UIHorizontalAlignment.Center;
            title.eventTextChanged += (_, _) => title.CenterToParent();
            title.text = T("Maintenance.Group:Allowed to despawn");
            title.MakePixelPerfect();

            var cancel = _header.AddUIComponent<UIButton>();
            cancel.normalBgSprite = "buttonclose";
            cancel.hoveredBgSprite = "buttonclosehover";
            cancel.pressedBgSprite = "buttonclosepressed";
            cancel.atlas = TextureUtil.Ingame;
            cancel.size = new Vector2(32, 32);
            cancel.relativePosition = new Vector2(_defaultWidth - 37, 4);
            cancel.eventClick += (_, _) => HandleClose();
        }

        private static string T(string text) {
            return Translation.Options.Get(text);
        }

        private void HandleClose() {
            if (!gameObject) return;

            if (UIView.GetModalComponent() == this) {
                UIView.PopModal();
                UIComponent modal = UIView.GetModalComponent();
                if (modal) {
                    UIView.GetAView().BringToFront(modal);
                } else {
                    UIView.GetAView().panelsLibraryModalEffect.Hide();
                }
            }

            _header = null;
            Destroy(gameObject);
            Log.Info("Allow Despawn panel closed and destroyed.");
        }
    }
}