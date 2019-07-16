namespace TrafficManager.Custom.Data {
    using System;
    using ColossalFramework;
    using ColossalFramework.Math;
    using JetBrains.Annotations;
    using Manager.Impl;
    using RedirectionFramework.Attributes;
    using UnityEngine;

    [TargetType(typeof(Vehicle))]
    [UsedImplicitly]
    public static class CustomVehicle {
        [RedirectMethod]
        [UsedImplicitly]
        public static void CustomSpawn(ref Vehicle vehicleData, ushort vehicleId) {
            var vehManager = Singleton<VehicleManager>.instance;
            var vehicleInfo = vehicleData.Info;
            if ((vehicleData.m_flags & Vehicle.Flags.Spawned) == 0) {
                vehicleData.m_flags |= Vehicle.Flags.Spawned;
                vehManager.AddToGrid(vehicleId, ref vehicleData, vehicleInfo.m_isLargeVehicle);
            }

            if (vehicleData.m_leadingVehicle == 0 && vehicleData.m_trailingVehicle != 0) {
                var trailingVehicle = vehicleData.m_trailingVehicle;
                var numIter = 0;
                while (trailingVehicle != 0) {
                    vehManager.m_vehicles.m_buffer[trailingVehicle].Spawn(trailingVehicle);
                    trailingVehicle = vehManager.m_vehicles.m_buffer[trailingVehicle].m_trailingVehicle;
                    if (++numIter > Constants.ServiceFactory.VehicleService.MaxVehicleCount) {
                        CODebugBase<LogChannel>.Error(LogChannel.Core,
                                                      $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                }
            }

            if (vehicleData.m_leadingVehicle == 0
                && vehicleData.m_trailingVehicle == 0
                && vehicleInfo.m_trailers != null)
            {
                var hasTrailers = vehicleInfo.m_vehicleAI.VerticalTrailers();
                var curVehicleId = vehicleId;
                var reversed = (vehManager.m_vehicles.m_buffer[curVehicleId].m_flags & Vehicle.Flags.Reversed) != 0;
                var lastFrameData = vehicleData.GetLastFrameData();
                var length = !hasTrailers ? vehicleInfo.m_generatedInfo.m_size.z * 0.5f : 0f;
                length -= (vehicleData.m_flags & Vehicle.Flags.Inverted) == (Vehicle.Flags)0
                               ? vehicleInfo.m_attachOffsetBack
                               : vehicleInfo.m_attachOffsetFront;
                var randomizer = new Randomizer(vehicleId);
                var trailerCount = 0;

                for (var i = 0; i < vehicleInfo.m_trailers.Length; i++) {
                    if (randomizer.Int32(100u) >= vehicleInfo.m_trailers[i].m_probability) {
                        continue;
                    }

                    var trailerInfo = vehicleInfo.m_trailers[i].m_info;
                    var inverted = randomizer.Int32(100u) < vehicleInfo.m_trailers[i].m_invertProbability;
                    length += !hasTrailers ? trailerInfo.m_generatedInfo.m_size.z * 0.5f : trailerInfo.m_generatedInfo.m_size.y;
                    length -= !inverted ? trailerInfo.m_attachOffsetFront : trailerInfo.m_attachOffsetBack;
                    var position = lastFrameData.m_position - (lastFrameData.m_rotation * new Vector3(
                                                                   0f,
                                                                   !hasTrailers ? 0f : length,
                                                                   !hasTrailers ? length : 0f));
                    if (vehManager.CreateVehicle(
                        out var trailerVehicleId,
                        ref Singleton<SimulationManager>.instance.m_randomizer,
                        trailerInfo,
                        position,
                        (TransferManager.TransferReason)vehicleData.m_transferType,
                        false,
                        false))
                    {
                        vehManager.m_vehicles.m_buffer[curVehicleId].m_trailingVehicle = trailerVehicleId;

                        var theVehicle = vehManager.m_vehicles.m_buffer[trailerVehicleId];

                        theVehicle.m_leadingVehicle = curVehicleId;
                        theVehicle.m_gateIndex = vehicleData.m_gateIndex;

                        if (inverted) {
                            theVehicle.m_flags |= Vehicle.Flags.Inverted;
                        }

                        if (reversed) {
                            theVehicle.m_flags |= Vehicle.Flags.Reversed;
                        }

                        theVehicle.m_frame0.m_rotation = lastFrameData.m_rotation;
                        theVehicle.m_frame1.m_rotation = lastFrameData.m_rotation;
                        theVehicle.m_frame2.m_rotation = lastFrameData.m_rotation;
                        theVehicle.m_frame3.m_rotation = lastFrameData.m_rotation;

                        trailerInfo.m_vehicleAI.FrameDataUpdated(
                            trailerVehicleId,
                            ref theVehicle,
                            ref theVehicle.m_frame0);

                        CustomVehicle.CustomSpawn(ref theVehicle, trailerVehicleId); // NON-STOCK CODE
                        curVehicleId = trailerVehicleId;
                    }

                    length += !hasTrailers ? trailerInfo.m_generatedInfo.m_size.z * 0.5f : 0f;
                    length -= !inverted ? trailerInfo.m_attachOffsetBack : trailerInfo.m_attachOffsetFront;
                    if (++trailerCount == vehicleInfo.m_maxTrailerCount) {
                        break;
                    }
                }
            }

            // NON-STOCK CODE START
            ExtVehicleManager.Instance.OnSpawnVehicle(vehicleId, ref vehicleData);
            // NON-STOCK CODE END
        }

        [RedirectMethod]
        [UsedImplicitly]
        public static void CustomUnspawn(ref Vehicle vehicleData, ushort vehicleId) {
            // NON-STOCK CODE START
            ExtVehicleManager.Instance.OnDespawnVehicle(vehicleId, ref vehicleData);

            // NON-STOCK CODE END
            var vehManager = Singleton<VehicleManager>.instance;
            if (vehicleData.m_leadingVehicle == 0 && vehicleData.m_trailingVehicle != 0) {
                var curVehicleId = vehicleData.m_trailingVehicle;
                vehicleData.m_trailingVehicle = 0;
                var numIters = 0;
                while (curVehicleId != 0) {
                    var trailingVehicleId = vehManager.m_vehicles.m_buffer[curVehicleId].m_trailingVehicle;
                    vehManager.m_vehicles.m_buffer[curVehicleId].m_leadingVehicle = 0;
                    vehManager.m_vehicles.m_buffer[curVehicleId].m_trailingVehicle = 0;
                    vehManager.ReleaseVehicle(curVehicleId);
                    curVehicleId = trailingVehicleId;
                    if (++numIters > 16384) {
                        CODebugBase<LogChannel>.Error(LogChannel.Core,
                                                      $"Invalid list detected!\n{Environment.StackTrace}");
                        break;
                    }
                }
            }

            if ((vehicleData.m_flags & Vehicle.Flags.Spawned) != 0) {
                var info = vehicleData.Info;
                if (info != null) {
                    vehManager.RemoveFromGrid(vehicleId, ref vehicleData, info.m_isLargeVehicle);
                }

                vehicleData.m_flags &= ~Vehicle.Flags.Spawned;
            }
        }
    }
}
