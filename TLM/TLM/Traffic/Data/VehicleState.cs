#define DEBUGVSTATEx
#define DEBUGREGx

using System;
using ColossalFramework;
using ColossalFramework.Math;
using CSUtil.Commons;
using TrafficManager.Custom.AI;
using TrafficManager.State;
using TrafficManager.State.ConfigData;
using UnityEngine;
using Random = UnityEngine.Random;

namespace TrafficManager.Traffic.Data {
        public struct VehicleState {
                public const int STATE_UPDATE_SHIFT = 6;
                public const int JUNCTION_RECHECK_SHIFT = 4;

                public ushort VehicleId;
                public uint LastPathId;
                public byte LastPathPositionIndex;
                public uint LastTransitStateUpdate;
                public uint LastPositionUpdate;
                public float TotalLength;
                public int WaitTime;
                public float ReduceSqrSpeedByValueToYield;
                public Flags VehicleFlags;
                public ExtVehicleType VehicleType;
                public bool IsHeavyVehicle;
                public bool IsRecklessDriver;
                public ushort CurrentSegmentId;
                public bool IsCurrentStartNode;
                public byte CurrentLaneIndex;
                public ushort NextSegmentId;
                public byte NextLaneIndex;
                public ushort PreviousVehicleIdOnSegment;
                public ushort NextVehicleIdOnSegment;
                public ushort LastAltLaneSelSegmentId;
                public byte TimedRand;

                // Dynamic Lane Selection
                public bool IsDlsReady;
                public float MaxReservedSpace;
                public float LaneSpeedRandInterval;
                public int MaxOptLaneChanges;
                public float MaxUnsafeSpeedDiff;
                public float MinSafeSpeedImprovement;
                public float MinSafeTrafficImprovement;

                private VehicleJunctionTransitState junctionTransitState_;

                internal VehicleState(ushort vehicleId) {
                        VehicleId = vehicleId;
                        LastPathId = 0;
                        LastPathPositionIndex = 0;
                        LastTransitStateUpdate = Now();
                        LastPositionUpdate = Now();
                        TotalLength = 0;
                        WaitTime = 0;
                        ReduceSqrSpeedByValueToYield = 0;
                        VehicleFlags = Flags.None;
                        VehicleType = ExtVehicleType.None;
                        IsHeavyVehicle = false;
                        IsRecklessDriver = false;
                        CurrentSegmentId = 0;
                        IsCurrentStartNode = false;
                        CurrentLaneIndex = 0;
                        NextSegmentId = 0;
                        NextLaneIndex = 0;
                        PreviousVehicleIdOnSegment = 0;
                        NextVehicleIdOnSegment = 0;
                        LastAltLaneSelSegmentId = 0;
                        junctionTransitState_ = VehicleJunctionTransitState.None;
                        TimedRand = 0;
                        IsDlsReady = false;
                        MaxReservedSpace = 0;
                        LaneSpeedRandInterval = 0;
                        MaxOptLaneChanges = 0;
                        MaxUnsafeSpeedDiff = 0;
                        MinSafeSpeedImprovement = 0;
                        MinSafeTrafficImprovement = 0;
                }

                [Flags]
                public enum Flags
                {
                        None = 0,
                        Created = 1,
                        Spawned = 1 << 1
                }

                public VehicleJunctionTransitState JunctionTransitState {
                        get { return junctionTransitState_; }
                        set {
                                if (value != junctionTransitState_) {
                                        LastTransitStateUpdate = Now();
                                }

                                junctionTransitState_ = value;
                        }
                }

                public float SqrVelocity {
                        get {
                                float ret = 0;
                                Constants.ServiceFactory.VehicleService.ProcessVehicle(
                                        VehicleId,
                                        (ushort vehId, ref Vehicle veh) => {
                                                ret = veh
                                                      .GetLastFrameVelocity()
                                                      .sqrMagnitude;
                                                return true;
                                        });
                                return ret;
                        }
                }

                public float Velocity {
                        get {
                                float ret = 0;
                                Constants.ServiceFactory.VehicleService.ProcessVehicle(
                                        VehicleId, (ushort vehId, ref Vehicle veh) => {
                                                ret = veh.GetLastFrameVelocity().magnitude;
                                                return true;
                                        });
                                return ret;
                        }
                }

                public override string ToString()
                {
                        return "[VehicleState\n" +
                               "\t" + $"vehicleId = {VehicleId}\n" +
                               "\t" + $"lastPathId = {LastPathId}\n" +
                               "\t" + $"lastPathPositionIndex = {LastPathPositionIndex}\n" +
                               "\t" + $"JunctionTransitState = {JunctionTransitState}\n" +
                               "\t" + $"lastTransitStateUpdate = {LastTransitStateUpdate}\n" +
                               "\t" + $"lastPositionUpdate = {LastPositionUpdate}\n" +
                               "\t" + $"totalLength = {TotalLength}\n" +
                               //"\t" + $"velocity = {velocity}\n" +
                               //"\t" + $"sqrVelocity = {sqrVelocity}\n" +
                               "\t" + $"waitTime = {WaitTime}\n" +
                               "\t" + $"reduceSqrSpeedByValueToYield = {ReduceSqrSpeedByValueToYield}\n" +
                               "\t" + $"flags = {VehicleFlags}\n" +
                               "\t" + $"vehicleType = {VehicleType}\n" +
                               "\t" + $"heavyVehicle = {IsHeavyVehicle}\n" +
                               "\t" + $"recklessDriver = {IsRecklessDriver}\n" +
                               "\t" + $"currentSegmentId = {CurrentSegmentId}\n" +
                               "\t" + $"currentStartNode = {IsCurrentStartNode}\n" +
                               "\t" + $"currentLaneIndex = {CurrentLaneIndex}\n" +
                               "\t" + $"nextSegmentId = {NextSegmentId}\n" +
                               "\t" + $"nextLaneIndex = {NextLaneIndex}\n" +
                               "\t" + $"previousVehicleIdOnSegment = {PreviousVehicleIdOnSegment}\n" +
                               "\t" + $"nextVehicleIdOnSegment = {NextVehicleIdOnSegment}\n" +
                               "\t" + $"lastAltLaneSelSegmentId = {LastAltLaneSelSegmentId}\n" +
                               "\t" + $"junctionTransitState = {junctionTransitState_}\n" +
                               "\t" + $"timedRand = {TimedRand}\n" +
                               "\t" + $"dlsReady = {IsDlsReady}\n" +
                               "\t" + $"maxReservedSpace = {MaxReservedSpace}\n" +
                               "\t" + $"laneSpeedRandInterval = {LaneSpeedRandInterval}\n" +
                               "\t" + $"maxOptLaneChanges = {MaxOptLaneChanges}\n" +
                               "\t" + $"maxUnsafeSpeedDiff = {MaxUnsafeSpeedDiff}\n" +
                               "\t" + $"minSafeSpeedImprovement = {MinSafeSpeedImprovement}\n" +
                               "\t" + $"minSafeTrafficImprovement = {MinSafeTrafficImprovement}\n" +
                               "VehicleState]";
                }

                public void UpdateDynamicLaneSelectionParameters() {
#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.UpdateDynamicLaneSelectionParameters({VehicleId}) called.");
                        }
#endif

                        if (!Options.IsDynamicLaneSelectionActive()) {
                                IsDlsReady = false;
                                return;
                        }

                        if (IsDlsReady) {
                                return;
                        }

                        var egoism = TimedRand / 100f;
                        var altruism = 1f - egoism;
                        var dls = GlobalConfig.Instance.DynamicLaneSelection;

                        if (Options.individualDrivingStyle) {
                                MaxReservedSpace = IsRecklessDriver
                                        ? Mathf.Lerp(dls.MinMaxRecklessReservedSpace,
                                                     dls.MaxMaxRecklessReservedSpace,
                                                     altruism)
                                        : Mathf.Lerp(dls.MinMaxReservedSpace,
                                                     dls.MaxMaxReservedSpace,
                                                     altruism);
                                LaneSpeedRandInterval = Mathf.Lerp(dls.MinLaneSpeedRandInterval,
                                                                   dls.MaxLaneSpeedRandInterval,
                                                                   egoism);
                                MaxOptLaneChanges = (int)Math.Round(
                                        Mathf.Lerp(dls.MinMaxOptLaneChanges,
                                                   dls.MaxMaxOptLaneChanges + 1,
                                                   egoism));
                                MaxUnsafeSpeedDiff = Mathf.Lerp(dls.MinMaxUnsafeSpeedDiff,
                                                                dls.MaxMaxOptLaneChanges,
                                                                egoism);
                                MinSafeSpeedImprovement = Mathf.Lerp(dls.MinMinSafeSpeedImprovement,
                                                                     dls.MaxMinSafeSpeedImprovement,
                                                                     altruism);
                                MinSafeTrafficImprovement = Mathf.Lerp(dls.MinMinSafeTrafficImprovement,
                                                                       dls.MaxMinSafeTrafficImprovement,
                                                                       altruism);
                        }
                        else {
                                MaxReservedSpace = IsRecklessDriver
                                                           ? dls.MaxRecklessReservedSpace
                                                           : dls.MaxReservedSpace;
                                LaneSpeedRandInterval = dls.LaneSpeedRandInterval;
                                MaxOptLaneChanges = dls.MaxOptLaneChanges;
                                MaxUnsafeSpeedDiff = dls.MaxUnsafeSpeedDiff;
                                MinSafeSpeedImprovement = dls.MinSafeSpeedImprovement;
                                MinSafeTrafficImprovement = dls.MinSafeTrafficImprovement;
                        }
                        IsDlsReady = true;
                }

                internal void Unlink() {
#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.Unlink({VehicleId}) called: " +
                                            $"Unlinking vehicle from all segment ends\nstate:{this}");
                        }
#endif

                        var vehStateManager = Constants.ManagerFactory.VehicleStateManager;

                        LastPositionUpdate = Now();

                        if (PreviousVehicleIdOnSegment != 0) {
                                vehStateManager.SetNextVehicleIdOnSegment(
                                        PreviousVehicleIdOnSegment,
                                        NextVehicleIdOnSegment); 
                                
                                // VehicleStates[previousVehicleIdOnSegment].nextVehicleIdOnSegment = nextVehicleIdOnSegment;
                        } else if (CurrentSegmentId != 0) {
                                var curEnd =
                                        Constants.ManagerFactory.SegmentEndManager.GetSegmentEnd(
                                                CurrentSegmentId, IsCurrentStartNode);
                                if (curEnd != null && curEnd.FirstRegisteredVehicleId == VehicleId) {
                                        curEnd.FirstRegisteredVehicleId = NextVehicleIdOnSegment;
                                }
                        }

                        if (NextVehicleIdOnSegment != 0) {
                                vehStateManager.SetPreviousVehicleIdOnSegment(
                                        NextVehicleIdOnSegment,
                                        PreviousVehicleIdOnSegment); 
                                
                                // .VehicleStates[nextVehicleIdOnSegment].previousVehicleIdOnSegment = previousVehicleIdOnSegment;
                        }

                        NextVehicleIdOnSegment = 0;
                        PreviousVehicleIdOnSegment = 0;

                        CurrentSegmentId = 0;
                        IsCurrentStartNode = false;
                        CurrentLaneIndex = 0;

                        LastPathId = 0;
                        LastPathPositionIndex = 0;

#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.Unlink({VehicleId}) finished: " +
                                            $"Unlinked vehicle from all segment ends\nstate:{this}");
                        }
#endif
                }

                internal void OnCreate(ref Vehicle vehicleData) {
#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.OnCreate({VehicleId}) called: {this}");
                        }
#endif

                        if ((VehicleFlags & Flags.Created) != Flags.None) {
#if DEBUG
                                if (GlobalConfig.Instance.Debug.Switches[9])
                                        Log._Debug($"VehicleState.OnCreate({VehicleId}): Vehicle is already created.");
#endif
                                OnRelease(ref vehicleData);
                        }

                        DetermineVehicleType(ref vehicleData);
                        ReduceSqrSpeedByValueToYield = Random.Range(256f, 784f);
                        IsRecklessDriver = false;
                        VehicleFlags = Flags.Created;

#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.OnCreate({VehicleId}) finished: {this}");
                        }
#endif
                }

                internal ExtVehicleType OnStartPathFind(ref Vehicle vehicleData, ExtVehicleType? vehicleType) {
#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.OnStartPathFind({VehicleId}, {vehicleType}) called: {this}");
                        }
#endif

                        if ((VehicleFlags & Flags.Created) == Flags.None) {
#if DEBUG
                                if (GlobalConfig.Instance.Debug.Switches[9]) {
                                        Log._Debug($"VehicleState.OnStartPathFind({VehicleId}, {vehicleType}): " +
                                                   "Vehicle has not yet been created.");
                                }
#endif
                                OnCreate(ref vehicleData);
                        }

                        if (vehicleType != null) {
                                VehicleType = (ExtVehicleType)vehicleType;
                        }

                        IsRecklessDriver =
                                Constants.ManagerFactory.VehicleBehaviorManager.IsRecklessDriver(
                                        VehicleId, ref vehicleData);
                        StepRand(true);
                        UpdateDynamicLaneSelectionParameters();

#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.OnStartPathFind({VehicleId}, {vehicleType}) finished: {this}");
                        }
#endif

                        return VehicleType;
                }

                /// <summary>
                /// Called from VehicleStateManager on vehicle spawn
                /// </summary>
                /// <param name="vehicleData">The connected vehicle</param>
                internal void OnSpawn(ref Vehicle vehicleData) {
#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.OnSpawn({VehicleId}) called: {this}");
                        }
#endif

                        if ((VehicleFlags & Flags.Created) == Flags.None) {
#if DEBUG
                                if (GlobalConfig.Instance.Debug.Switches[9]) {
                                        Log._Debug($"VehicleState.OnSpawn({VehicleId}): " +
                                                   "Vehicle has not yet been created.");
                                }
#endif
                                OnCreate(ref vehicleData);
                        }

                        Unlink();

                        LastPathId = 0;
                        LastPathPositionIndex = 0;
                        LastAltLaneSelSegmentId = 0;

                        IsRecklessDriver =
                                Constants.ManagerFactory.VehicleBehaviorManager.IsRecklessDriver(
                                        VehicleId, ref vehicleData);
                        StepRand(true);
                        UpdateDynamicLaneSelectionParameters();

                        try {
                                TotalLength = vehicleData.CalculateTotalLength(VehicleId);
                        }
#if DEBUG
                        catch (Exception e) {
#else
                        catch (Exception) {
#endif
                                TotalLength = 0;
#if DEBUG
                                if (GlobalConfig.Instance.Debug.Switches[9]) {
                                        Log._Debug($"VehicleState.OnSpawn({VehicleId}): Error occurred while " +
                                                   $"calculating total length: {e}\nstate: {this}");
                                }
#endif
                                return;
                        }

                        VehicleFlags |= Flags.Spawned;

#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.OnSpawn({VehicleId}) finished: {this}");
                        }
#endif
                }

                internal void UpdatePosition(ref Vehicle vehicleData,
                                             ref PathUnit.Position curPos,
                                             ref PathUnit.Position nextPos, 
                                             bool skipCheck = false) {
#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.UpdatePosition({VehicleId}) called: {this}");
                        }
#endif

                        if ((VehicleFlags & Flags.Spawned) == Flags.None) {
#if DEBUG
                                if (GlobalConfig.Instance.Debug.Switches[9]) { 
                                        Log._Debug($"VehicleState.UpdatePosition({VehicleId}): Vehicle is not yet spawned.");
                                }
#endif
                                OnSpawn(ref vehicleData);
                        }

                        if (NextSegmentId != nextPos.m_segment || NextLaneIndex != nextPos.m_lane) {
                                NextSegmentId = nextPos.m_segment;
                                NextLaneIndex = nextPos.m_lane;
                        }

                        var startNode = IsTransitNodeCurStartNode(ref curPos, ref nextPos);
                        var end = Constants.ManagerFactory.SegmentEndManager.GetSegmentEnd(curPos.m_segment, startNode);

                        if (end == null || CurrentSegmentId != end.SegmentId || IsCurrentStartNode != end.StartNode ||
                            CurrentLaneIndex != curPos.m_lane) {
#if DEBUG
                                if (GlobalConfig.Instance.Debug.Switches[9]) {
                                        Log._Debug($"VehicleState.UpdatePosition({VehicleId}): Current segment end changed. " +
                                                   $"seg. {CurrentSegmentId}, start {IsCurrentStartNode}, lane {CurrentLaneIndex} -> " +
                                                   $"seg. {end?.SegmentId}, start {end?.StartNode}, lane {curPos.m_lane}");
                                }
#endif
                                if (CurrentSegmentId != 0) {
#if DEBUG
                                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                                Log._Debug($"VehicleState.UpdatePosition({VehicleId}): " +
                                                           "Unlinking from current segment end");
                                        }
#endif
                                        Unlink();
                                }

                                LastPathId = vehicleData.m_path;
                                LastPathPositionIndex = vehicleData.m_pathPositionIndex;

                                CurrentSegmentId = curPos.m_segment;
                                IsCurrentStartNode = startNode;
                                CurrentLaneIndex = curPos.m_lane;

                                WaitTime = 0;
                                if (end != null) {
                                        if (vehicleData.m_leadingVehicle == 0) {
                                                StepRand(false);
                                        }

#if DEBUGVSTATE
                                        if (GlobalConfig.Instance.Debug.Switches[9])
                                                Log._Debug($"VehicleState.UpdatePosition({vehicleId}): "+
                                                           $"Linking vehicle to segment end {end.SegmentId} @ {end.StartNode} "+
                                                           $"({end.NodeId}). Current position: Seg. {curPos.m_segment}, "+
                                                           $"lane {curPos.m_lane}, offset {curPos.m_offset} / Next position: "+
                                                           $"Seg. {nextPos.m_segment}, lane {nextPos.m_lane}, offset {nextPos.m_offset}");
#endif
                                        Link(end);
                                        JunctionTransitState = VehicleJunctionTransitState.Approach;
                                } else {
                                        JunctionTransitState = VehicleJunctionTransitState.None;
                                }
                        }
#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.UpdatePosition({VehicleId}) finshed: {this}");
                        }
#endif
                }

                /// <summary>Called from VehicleStateManager on despawn</summary>
                internal void OnDespawn() {
#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.OnDespawn({VehicleId} called: {this}");
                        }
#endif
                        if ((VehicleFlags & Flags.Spawned) == Flags.None) {
#if DEBUG
                                if (GlobalConfig.Instance.Debug.Switches[9]) {
                                        Log._Debug($"VehicleState.OnDespawn({VehicleId}): Vehicle is not spawned.");
                                }
#endif
                                return;
                        }

                        Constants.ManagerFactory.ExtCitizenInstanceManager.ResetInstance(
                                CustomPassengerCarAI.GetDriverInstanceId(
                                        VehicleId,
                                        ref Singleton<VehicleManager>
                                            .instance.m_vehicles.m_buffer[VehicleId]));

                        Unlink();

                        CurrentSegmentId = 0;
                        IsCurrentStartNode = false;
                        CurrentLaneIndex = 0;
                        LastAltLaneSelSegmentId = 0;
                        IsRecklessDriver = false;

                        NextSegmentId = 0;
                        NextLaneIndex = 0;

                        TotalLength = 0;

                        VehicleFlags &= ~Flags.Spawned;

#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.OnDespawn({VehicleId}) finished: {this}");
                        }
#endif
                }

                internal void OnRelease(ref Vehicle vehicleData) {
#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.OnRelease({VehicleId}) called: {this}");
                        }
#endif

                        if ((VehicleFlags & Flags.Created) == Flags.None) {
#if DEBUG
                                if (GlobalConfig.Instance.Debug.Switches[9]) {
                                        Log._Debug($"VehicleState.OnRelease({VehicleId}): Vehicle is not created.");
                                }
#endif
                                return;
                        }

                        if ((VehicleFlags & Flags.Spawned) != Flags.None) {
#if DEBUG
                                if (GlobalConfig.Instance.Debug.Switches[9]) {
                                        Log._Debug($"VehicleState.OnRelease({VehicleId}): Vehicle is spawned.");
                                }
#endif
                                OnDespawn();
                        }

                        LastPathId = 0;
                        LastPathPositionIndex = 0;
                        LastTransitStateUpdate = Now();
                        LastPositionUpdate = Now();
                        WaitTime = 0;
                        ReduceSqrSpeedByValueToYield = 0;
                        VehicleFlags = Flags.None;
                        VehicleType = ExtVehicleType.None;
                        IsHeavyVehicle = false;
                        PreviousVehicleIdOnSegment = 0;
                        NextVehicleIdOnSegment = 0;
                        LastAltLaneSelSegmentId = 0;
                        junctionTransitState_ = VehicleJunctionTransitState.None;
                        IsRecklessDriver = false;
                        MaxReservedSpace = 0;
                        LaneSpeedRandInterval = 0;
                        MaxOptLaneChanges = 0;
                        MaxUnsafeSpeedDiff = 0;
                        MinSafeSpeedImprovement = 0;
                        MinSafeTrafficImprovement = 0;

#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.OnRelease({VehicleId}) finished: {this}");
                        }
#endif
                }

                /// <summary>
                /// Determines if the junction transit state has been recently modified
                /// </summary>
                /// <returns></returns>
                internal bool IsJunctionTransitStateNew() {
                        var frame = Constants.ServiceFactory.SimulationService.CurrentFrameIndex;
                        return (LastTransitStateUpdate >> STATE_UPDATE_SHIFT) >= (frame >> STATE_UPDATE_SHIFT);
                }

                private static ushort GetTransitNodeId(ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
                        var startNode = IsTransitNodeCurStartNode(ref curPos, ref nextPos);
                        ushort transitNodeId1 = 0;
                        Constants.ServiceFactory.NetService.ProcessSegment(
                                curPos.m_segment,
                                (ushort segmentId, ref NetSegment segment) => {
                                        transitNodeId1 = startNode ? segment.m_startNode : segment.m_endNode;
                                        return true;
                                });

                        ushort transitNodeId2 = 0;
                        Constants.ServiceFactory.NetService.ProcessSegment(
                                nextPos.m_segment,
                                (ushort segmentId, ref NetSegment segment) => {
                                        transitNodeId2 = startNode ? segment.m_startNode : segment.m_endNode;
                                        return true;
                                });

                        return transitNodeId1 != transitNodeId2 ? (ushort)0 : transitNodeId1;
                }

                private static bool IsTransitNodeCurStartNode(ref PathUnit.Position curPos, ref PathUnit.Position nextPos) {
                        // note: does not check if curPos and nextPos are successive path positions
                        bool startNode;
                        if (curPos.m_offset == 0) {
                                startNode = true;
                        }
                        else if (curPos.m_offset == 255) {
                                startNode = false;
                        }
                        else if (nextPos.m_offset == 0) {
                                startNode = true;
                        }
                        else {
                                startNode = false;
                        }
                        return startNode;
                }

                private static uint Now() {
                        return Constants.ServiceFactory.SimulationService.CurrentFrameIndex;
                }

                private void Link(ISegmentEnd end) {
#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.Link({VehicleId}) called: " +
                                           $"Linking vehicle to segment end {end}\nstate:{this}");
                        }
#endif

                        var oldFirstRegVehicleId = end.FirstRegisteredVehicleId;
                        if (oldFirstRegVehicleId != 0) {
                                Constants.ManagerFactory.VehicleStateManager.SetPreviousVehicleIdOnSegment(oldFirstRegVehicleId, VehicleId);
                                // VehicleStates[oldFirstRegVehicleId].previousVehicleIdOnSegment = vehicleId;
                                NextVehicleIdOnSegment = oldFirstRegVehicleId;
                        }
                        end.FirstRegisteredVehicleId = VehicleId;

#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.Link({VehicleId}) finished: " +
                                           $"Linked vehicle to segment end {end}\nstate:{this}");
                        }
#endif
                }

                private void StepRand(bool init) {
                        var rand = Constants.ServiceFactory.SimulationService.Randomizer;
                        if (init || rand.UInt32(GlobalConfig.Instance.Gameplay.VehicleTimedRandModulo) == 0) {
                                TimedRand = Options.individualDrivingStyle 
                                                    ? (byte)rand.UInt32(100) 
                                                    : (byte)50;
                        }
                }
      
                private void DetermineVehicleType(ref Vehicle vehicleData) {
                        var ai = vehicleData.Info.m_vehicleAI;

                        if ((vehicleData.m_flags & Vehicle.Flags.Emergency2) != 0) {
                                VehicleType = ExtVehicleType.Emergency;
                        } else {
                                var type = DetermineVehicleTypeFromAIType(ai, false);
                                if (type != null) {
                                        VehicleType = (ExtVehicleType)type;
                                } else {
                                        VehicleType = ExtVehicleType.None;
                                }
                        }

                        IsHeavyVehicle = VehicleType == ExtVehicleType.CargoTruck 
                                         && ((CargoTruckAI)ai).m_isHeavyVehicle;

#if DEBUG
                        if (GlobalConfig.Instance.Debug.Switches[9]) {
                                Log._Debug($"VehicleState.DetermineVehicleType({VehicleId}): " +
                                            $"vehicleType={VehicleType}, heavyVehicle={IsHeavyVehicle}. " +
                                            $"Info={vehicleData.Info?.name}");
                        }
#endif
                }

                private ExtVehicleType? DetermineVehicleTypeFromAIType(VehicleAI ai, bool emergencyOnDuty) {
                        if (emergencyOnDuty) {
                                return ExtVehicleType.Emergency;
                        }

                        switch (ai.m_info.m_vehicleType) {
                                case VehicleInfo.VehicleType.Bicycle:
                                        return ExtVehicleType.Bicycle;
                                case VehicleInfo.VehicleType.Car:
                                        if (ai is PassengerCarAI) {
                                                return ExtVehicleType.PassengerCar;
                                        } else if (ai is AmbulanceAI || ai is FireTruckAI || ai is PoliceCarAI ||
                                                   ai is HearseAI || ai is GarbageTruckAI ||
                                                   ai is MaintenanceTruckAI || ai is SnowTruckAI ||
                                                   ai is WaterTruckAI || ai is DisasterResponseVehicleAI ||
                                                   ai is ParkMaintenanceVehicleAI || ai is PostVanAI) {
                                                return ExtVehicleType.Service;
                                        } else if (ai is CarTrailerAI) {
                                                return ExtVehicleType.None;
                                        } else if (ai is BusAI) {
                                                return ExtVehicleType.Bus;
                                        } else if (ai is TaxiAI) {
                                                return ExtVehicleType.Taxi;
                                        } else if (ai is CargoTruckAI) {
                                                return ExtVehicleType.CargoTruck;
                                        }

                                        break;
                                case VehicleInfo.VehicleType.Metro:
                                case VehicleInfo.VehicleType.Train:
                                case VehicleInfo.VehicleType.Monorail:
                                        return ai is CargoTrainAI 
                                                       ? ExtVehicleType.CargoTrain 
                                                       : ExtVehicleType.PassengerTrain;

                                case VehicleInfo.VehicleType.Tram:
                                        return ExtVehicleType.Tram;
                                case VehicleInfo.VehicleType.Ship:
                                        return ai is PassengerShipAI 
                                                       ? ExtVehicleType.PassengerShip 
                                                       : ExtVehicleType.CargoShip;

                                        //if (ai is CargoShipAI)
                                //break;
                                case VehicleInfo.VehicleType.Plane:
                                        if (ai is PassengerPlaneAI) {
                                                return ExtVehicleType.PassengerPlane;
                                        } else if (ai is CargoPlaneAI) {
                                                return ExtVehicleType.CargoPlane;
                                        }

                                        break;
                                case VehicleInfo.VehicleType.Helicopter:
                                        //if (ai is PassengerPlaneAI)
                                        return ExtVehicleType.Helicopter;
                                //break;
                                case VehicleInfo.VehicleType.Ferry:
                                        return ExtVehicleType.Ferry;
                                case VehicleInfo.VehicleType.Blimp:
                                        return ExtVehicleType.Blimp;
                                case VehicleInfo.VehicleType.CableCar:
                                        return ExtVehicleType.CableCar;
                        }
#if DEBUGVSTATE
                        Log._Debug($"VehicleState.DetermineVehicleType({vehicleId}): "+
                                   $"Could not determine vehicle type from ai type: {ai.GetType().ToString()}");
#endif
                        return null;
                }
        }
}
