using System.Reflection;
using ICities;
using TrafficManager.CustomAI;
using UnityEngine;

namespace TrafficManager
{
    public sealed class ThreadingExtension : ThreadingExtensionBase
    {
        public override void OnUpdate(float realTimeDelta, float simulationTimeDelta)
        {
            base.OnUpdate(realTimeDelta, simulationTimeDelta);

            Debug.Log("Checking for NULL LoadingExtension Instance");
            if (LoadingExtension.Instance == null)
            {
                return;
            }

            Debug.Log("Getting ToolMode");
            if (LoadingExtension.Instance.ToolMode != TrafficManagerMode.None &&
                ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficLightTool)
            {
                Debug.Log("Closing UI");
                LoadingExtension.Instance.UI.Close();
            }

            Debug.Log("Checking if TrafficLightTool is Visible");
            if (ToolsModifierControl.toolController.CurrentTool != LoadingExtension.Instance.TrafficLightTool &&
                LoadingExtension.Instance.UI.IsVisible())
            {
                Debug.Log("Closing UI");
                LoadingExtension.Instance.UI.Close();
            }

            Debug.Log("If !DetourInited");
            if (!LoadingExtension.Instance.DetourInited)
            {
                Debug.Log("Redirecting Car AI Calls");
                LoadingExtension.Instance.RevertMethods[0] = RedirectionHelper.RedirectCalls(
                    typeof (CarAI).GetMethod("CalculateSegmentPosition",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[]
                        {
                            typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
                            typeof (PathUnit.Position), typeof (uint), typeof (byte), typeof (PathUnit.Position),
                            typeof (uint), typeof (byte), typeof (Vector3).MakeByRefType(),
                            typeof (Vector3).MakeByRefType(), typeof (float).MakeByRefType()
                        },
                        null),
                    typeof (CustomCarAI).GetMethod("CalculateSegmentPosition"));

                Debug.Log("Redirecting SimulationStep");
                LoadingExtension.Instance.RevertMethods[1] = RedirectionHelper.RedirectCalls(
                    typeof (RoadBaseAI).GetMethod("SimulationStep",
                        new[] {typeof (ushort), typeof (NetNode).MakeByRefType()}),
                    typeof (CustomRoadAI).GetMethod("SimulationStep", BindingFlags.NonPublic | BindingFlags.Instance));

                Debug.Log("Redirecting Human AI Calls");
                LoadingExtension.Instance.RevertMethods[2] =
                    RedirectionHelper.RedirectCalls(typeof (HumanAI).GetMethod("CheckTrafficLights",
                        BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        new[] {typeof (ushort), typeof (ushort)},
                        null),
                        typeof (CustomHumanAI).GetMethod("CheckTrafficLights"));

                if (!LoadingExtension.PathfinderIncompatibility)
                {
                    LoadingExtension.Instance.RevertMethods[3] =
                        RedirectionHelper.RedirectCalls(
                            typeof (CarAI).GetMethod("SimulationStep",
                                new[] {typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (Vector3)}),
                            typeof (CustomCarAI).GetMethod("SimulationStep",
                                BindingFlags.NonPublic | BindingFlags.Instance));


                    LoadingExtension.Instance.RevertMethods[4] =
                        RedirectionHelper.RedirectCalls(
                            typeof (PassengerCarAI).GetMethod("SimulationStep",
                                new[] {typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (Vector3)}),
                            typeof (CustomPassengerCarAI).GetMethod("SimulationStep",
                                BindingFlags.NonPublic | BindingFlags.Instance));

                    LoadingExtension.Instance.RevertMethods[5] =
                        RedirectionHelper.RedirectCalls(
                            typeof (CargoTruckAI).GetMethod("SimulationStep",
                                new[] {typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (Vector3)}),
                            typeof (CustomCargoTruckAI).GetMethod("SimulationStep",
                                BindingFlags.NonPublic | BindingFlags.Instance));

                    LoadingExtension.Instance.RevertMethods[6] =
                        RedirectionHelper.RedirectCalls(typeof (CarAI).GetMethod("CalculateSegmentPosition",
                            BindingFlags.NonPublic | BindingFlags.Instance,
                            null,
                            new[]
                            {
                                typeof (ushort), typeof (Vehicle).MakeByRefType(), typeof (PathUnit.Position),
                                typeof (uint),
                                typeof (byte), typeof (Vector3).MakeByRefType(), typeof (Vector3).MakeByRefType(),
                                typeof (float).MakeByRefType()
                            },
                            null),
                            typeof (CustomCarAI).GetMethod("CalculateSegmentPosition2"));
                }

                LoadingExtension.Instance.DetourInited = true;
            }

            if (!LoadingExtension.Instance.NodeSimulationLoaded)
            {
                LoadingExtension.Instance.NodeSimulationLoaded = true;
                ToolsModifierControl.toolController.gameObject.AddComponent<CustomRoadAI>();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LoadingExtension.Instance.UI.Close();
            }
        }
    }
}
