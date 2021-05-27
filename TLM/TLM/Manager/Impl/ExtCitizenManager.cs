﻿namespace TrafficManager.Manager.Impl {
    using ColossalFramework;
    using CSUtil.Commons;
    using System.Collections.Generic;
    using System;
    using TrafficManager.API.Manager;
    using TrafficManager.API.Traffic.Data;
    using TrafficManager.API.Traffic.Enums;

#if DEBUG
    using State.ConfigData;
#endif

    public class ExtCitizenManager
        : AbstractCustomManager,
          ICustomDataManager<List<Configuration.ExtCitizenData>>,
          IExtCitizenManager
    {
        private ExtCitizenManager() {
            ExtCitizens = new ExtCitizen[CitizenManager.MAX_CITIZEN_COUNT];

            for (uint i = 0; i < CitizenManager.MAX_CITIZEN_COUNT; ++i) {
                ExtCitizens[i] = new ExtCitizen(i);
                Reset(ref ExtCitizens[i]);
            }
        }

        public static ExtCitizenManager Instance = new ExtCitizenManager();

        /// <summary>
        /// All additional data for citizens. Index: citizen id
        /// </summary>
        public ExtCitizen[] ExtCitizens { get; }

        protected override void InternalPrintDebugInfo() {
            base.InternalPrintDebugInfo();
            Log._Debug($"Extended citizen data:");

            for (var i = 0; i < ExtCitizens.Length; ++i) {
                if (!IsValid((uint)i)) {
                    continue;
                }

                Log._Debug($"Citizen {i}: {ExtCitizens[i]}");
            }
        }

        public void OnReleaseCitizen(uint citizenId) {
            Reset(ref ExtCitizens[citizenId]);
        }

        public void OnArriveAtDestination(uint citizenId, ref Citizen citizenData, ref CitizenInstance instanceData) {
#if DEBUG
            bool citizenDebug = DebugSettings.CitizenId == 0 || DebugSettings.CitizenId == citizenId;

            // bool logParkingAi = DebugSwitch.BasicParkingAILog.Get() && citizenDebug;
            bool extendedLogParkingAi = DebugSwitch.ExtendedParkingAILog.Get() && citizenDebug;

            if (extendedLogParkingAi) {
                Log._Debug($"ExtCitizenManager.OnArriveAtDestination({citizenId}) called");
            }
#else
            const bool extendedLogParkingAi = false;
#endif
            if (ExtCitizens[citizenId].lastLocation == Citizen.Location.Home) {
                if (extendedLogParkingAi) {
                    Log._Debug(
                        $"ExtCitizenManager.OnArriveAtDestination({citizenId}): setting " +
                        $"lastTransportMode=transportMode={ExtCitizens[citizenId].transportMode}");
                }

                ExtCitizens[citizenId].lastTransportMode = ExtCitizens[citizenId].transportMode;
            }

            ExtCitizens[citizenId].transportMode = ExtTransportMode.None;

            ushort targetBuildingId = instanceData.m_targetBuilding;
            Services.CitizenService.ProcessCitizen(
                citizenId,
                (uint citId, ref Citizen cit) => {
                    cit.SetLocationByBuilding(citId, targetBuildingId);
                    return true;
                });

            if (citizenData.CurrentLocation != Citizen.Location.Moving) {
                ExtCitizens[citizenId].lastLocation = citizenData.CurrentLocation;
                if (extendedLogParkingAi) {
                    Log._Debug(
                        $"ExtCitizenManager.OnArriveAtDestination({citizenId}): setting " +
                        $"lastLocation={ExtCitizens[citizenId].lastLocation}");
                }
            }
        }

        public void ResetCitizen(uint citizenId) {
            Reset(ref ExtCitizens[citizenId]);
        }

        private void Reset(ref ExtCitizen extCitizen) {
            extCitizen.transportMode = ExtTransportMode.None;
            extCitizen.lastTransportMode = ExtTransportMode.None;
            ResetLastLocation(ref extCitizen);
        }

        private bool IsValid(uint citizenId) {
            return Constants.ServiceFactory.CitizenService.IsCitizenValid(citizenId);
        }

        private void ResetLastLocation(ref ExtCitizen extCitizen) {
            var loc = Citizen.Location.Moving;
            Constants.ServiceFactory.CitizenService.ProcessCitizen(
                extCitizen.citizenId,
                (uint citId, ref Citizen citizen) => {
                    loc = citizen.CurrentLocation;
                    return true;
                });
            extCitizen.lastLocation = loc;
        }

        // stock code
        public Citizen.AgeGroup GetAgeGroup(Citizen.AgePhase agePhase) {
            switch (agePhase) {
                case Citizen.AgePhase.Child:
                    return Citizen.AgeGroup.Child;
                case Citizen.AgePhase.Teen0:
                case Citizen.AgePhase.Teen1:
                    return Citizen.AgeGroup.Teen;
                case Citizen.AgePhase.Young0:
                case Citizen.AgePhase.Young1:
                case Citizen.AgePhase.Young2:
                    return Citizen.AgeGroup.Young;
                case Citizen.AgePhase.Adult0:
                case Citizen.AgePhase.Adult1:
                case Citizen.AgePhase.Adult2:
                case Citizen.AgePhase.Adult3:
                    return Citizen.AgeGroup.Adult;
                case Citizen.AgePhase.Senior0:
                case Citizen.AgePhase.Senior1:
                case Citizen.AgePhase.Senior2:
                case Citizen.AgePhase.Senior3:
                    return Citizen.AgeGroup.Senior;
                default:
                    return Citizen.AgeGroup.Adult;
            }
        }

        public override void OnLevelUnloading() {
            base.OnLevelUnloading();
            Reset();
        }

        internal void Reset() {
            for (int i = 0; i < ExtCitizens.Length; ++i) {
                Reset(ref ExtCitizens[i]);
            }
        }

        public bool LoadData(List<Configuration.ExtCitizenData> data) {
            bool success = true;
            Log.Info($"Loading {data.Count} extended citizens");

            foreach (Configuration.ExtCitizenData item in data) {
                try {
                    uint citizenId = item.citizenId;
                    ExtCitizens[citizenId].transportMode = (ExtTransportMode)item.lastTransportMode;
                }
                catch (Exception e) {
                    // ignore, as it's probably corrupt save data. it'll be culled on next save
                    Log.Warning("Error loading ext. citizen: " + e.ToString());
                    success = false;
                }
            }

            return success;
        }

        public List<Configuration.ExtCitizenData> SaveData(ref bool success) {
            List<Configuration.ExtCitizenData> ret = new List<Configuration.ExtCitizenData>();
            for (uint citizenId = 0; citizenId < CitizenManager.MAX_CITIZEN_COUNT; ++citizenId) {
                try {
                    if ((Singleton<CitizenManager>.instance.m_citizens.m_buffer[citizenId].m_flags &
                         Citizen.Flags.Created) == Citizen.Flags.None) {
                        continue;
                    }

                    if (ExtCitizens[citizenId].transportMode == ExtTransportMode.None) {
                        continue;
                    }

                    Configuration.ExtCitizenData item = new Configuration.ExtCitizenData(citizenId);
                    item.lastTransportMode = (int)ExtCitizens[citizenId].transportMode;
                    ret.Add(item);
                }
                catch (Exception ex) {
                    Log.Error($"Exception occurred while saving ext. citizen @ {citizenId}: {ex}");
                    success = false;
                }
            }

            return ret;
        }
    }
}