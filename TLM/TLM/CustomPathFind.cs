using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using ColossalFramework.UI;
using TrafficManager.Traffic;
using UnityEngine;

namespace TrafficManager
{
    public class CustomPathFind : PathFind
    {
        private struct BufferItem
        {
            public PathUnit.Position Position;
            public float ComparisonValue;
            public float MethodDistance;
            public uint LaneId;
            public NetInfo.Direction Direction;
            public NetInfo.LaneType LanesUsed;
        }

        public CustomPathFind()
        {

        }

        //Expose the private fields
        FieldInfo _fieldpathUnits;
        FieldInfo _fieldQueueFirst;
        FieldInfo _fieldQueueLast;
        FieldInfo _fieldQueueLock;
        FieldInfo _fieldCalculating;
        FieldInfo _fieldTerminated;
        FieldInfo _fieldPathFindThread;

        private Array32<PathUnit> PathUnits
        {
            get { return _fieldpathUnits.GetValue(this) as Array32<PathUnit>; }
            set { _fieldpathUnits.SetValue(this, value); }
        }

        private uint QueueFirst
        {
            get { return (uint)_fieldQueueFirst.GetValue(this); }
            set { _fieldQueueFirst.SetValue(this, value); }
        }

        private uint QueueLast
        {
            get { return (uint)_fieldQueueLast.GetValue(this); }
            set { _fieldQueueLast.SetValue(this, value); }
        }

        private uint Calculating
        {
            get { return (uint)_fieldCalculating.GetValue(this); }
            set { _fieldCalculating.SetValue(this, value); }
        }

        private object QueueLock
        {
            get { return _fieldQueueLock.GetValue(this); }
            set { _fieldQueueLock.SetValue(this, value); }
        }

        private object _bufferLock;
        private Thread CustomPathFindThread
        {
            get { return (Thread)_fieldPathFindThread.GetValue(this); }
            set { _fieldPathFindThread.SetValue(this, value); }
        }

        private bool Terminated
        {
            get { return (bool)_fieldTerminated.GetValue(this); }
            set { _fieldTerminated.SetValue(this, value); }
        }
        private int _bufferMinPos;
        private int _bufferMaxPos;
        private uint[] _laneLocation;
        private PathUnit.Position[] _laneTarget;
        private BufferItem[] _buffer;
        private int[] _bufferMin;
        private int[] _bufferMax;
        private float _maxLength;
        private uint _startLaneA;
        private uint _startLaneB;
        private uint _endLaneA;
        private uint _endLaneB;
        private uint _vehicleLane;
        private byte _startOffsetA;
        private byte _startOffsetB;
        private byte _vehicleOffset;
        private bool _isHeavyVehicle;
        private bool _ignoreBlocked;
        private bool _stablePath;
/*
        private TrafficRoadRestrictions.VehicleTypes _vehicleType;
*/
        private Randomizer _pathRandomizer;
        private uint _pathFindIndex;
        private NetInfo.LaneType _laneTypes;
        private VehicleInfo.VehicleType _vehicleTypes;


        protected virtual void Awake()
        {
            var stockPathFindType = typeof(PathFind);
            const BindingFlags fieldFlags = BindingFlags.NonPublic | BindingFlags.Instance;


            _fieldpathUnits = stockPathFindType.GetField("_pathUnits", fieldFlags);
            _fieldQueueFirst = stockPathFindType.GetField("QueueFirst", fieldFlags);
            _fieldQueueLast = stockPathFindType.GetField("QueueLast", fieldFlags);
            _fieldQueueLock = stockPathFindType.GetField("QueueLock", fieldFlags);
            _fieldTerminated = stockPathFindType.GetField("Terminated", fieldFlags);
            _fieldCalculating = stockPathFindType.GetField("Calculating", fieldFlags);
            _fieldPathFindThread = stockPathFindType.GetField("PathFindThread", fieldFlags);

            _buffer = new BufferItem[65536];
            _bufferLock = PathManager.instance.m_bufferLock;
            PathUnits = PathManager.instance.m_pathUnits;
            QueueLock = new object();
            _laneLocation = new uint[262144];
            _laneTarget = new PathUnit.Position[262144];
            _bufferMin = new int[1024];
            _bufferMax = new int[1024];

            m_pathfindProfiler = new ThreadProfiler();

            CustomPathFindThread = new Thread(PathFindThread) {Name = "Pathfind"};
            CustomPathFindThread.Start();
            if (!CustomPathFindThread.IsAlive)
            {
                //CODebugBase<LogChannel>.Error(LogChannel.Core, "Path find thread failed to start!");
                Debug.LogError("Path find thread failed to start!", this);
            }

        }

        //Unmodified from stock
        protected virtual void OnDestroy()
        {
            while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                Terminated = true;
                Monitor.PulseAll(QueueLock);
            }
            finally
            {
                Monitor.Exit(QueueLock);
            }
        }

        //Stock code
        public new bool CalculatePath(uint unit, bool skipQueue)
        {
            if (Singleton<PathManager>.instance.AddPathReference(unit))
            {
                while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    if (skipQueue)
                    {
                        if (QueueLast == 0u)
                        {
                            QueueLast = unit;
                        }
                        else
                        {
                            PathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = QueueFirst;
                        }
                        QueueFirst = unit;
                    }
                    else
                    {
                        if (QueueLast == 0u)
                        {
                            QueueFirst = unit;
                        }
                        else
                        {
                            PathUnits.m_buffer[(int)((UIntPtr)QueueLast)].m_nextPathUnit = unit;
                        }
                        QueueLast = unit;
                    }
                    var exprBdCp0 = PathUnits.m_buffer;
                    var exprBdCp1 = (UIntPtr)unit;
                    exprBdCp0[(int)exprBdCp1].m_pathFindFlags = (byte)(exprBdCp0[(int)exprBdCp1].m_pathFindFlags | 1);
                    m_queuedPathFindCount++;
                    Monitor.Pulse(QueueLock);
                }
                finally
                {
                    Monitor.Exit(QueueLock);
                }
                return true;
            }
            return false;
        }

        //Stock code
        public new void WaitForAllPaths()
        {
            while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            try
            {
                while ((QueueFirst != 0u || Calculating != 0u) && !Terminated)
                {
                    Monitor.Wait(QueueLock);
                }
            }
            finally
            {
                Monitor.Exit(QueueLock);
            }
        }


        private void PathFindImplementation(uint unit, ref PathUnit data)
        {
            var instance = Singleton<NetManager>.instance;
            _laneTypes = (NetInfo.LaneType)PathUnits.m_buffer[(int)((UIntPtr)unit)].m_laneTypes;
            _vehicleTypes = (VehicleInfo.VehicleType)PathUnits.m_buffer[(int)((UIntPtr)unit)].m_vehicleTypes;
            _maxLength = PathUnits.m_buffer[(int)((UIntPtr)unit)].m_length;
            _pathFindIndex = (_pathFindIndex + 1u & 32767u);
            _pathRandomizer = new Randomizer(unit);
            _isHeavyVehicle = ((PathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 16) != 0);
            _ignoreBlocked = ((PathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 32) != 0);
            _stablePath = ((PathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags & 64) != 0);
            //this._vehicleType =
            //    TrafficRoadRestrictions.vehicleType(this._pathUnits._buffer[(int) ((UIntPtr) unit)].m_simulationFlags);

            var num = PathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount & 15;
            var num2 = PathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount >> 4;
            BufferItem startPosA;
            if (data.m_position00.m_segment != 0 && num >= 1)
            {
                _startLaneA = PathManager.GetLaneID(data.m_position00);
                _startOffsetA = data.m_position00.m_offset;
                startPosA.LaneId = _startLaneA;
                startPosA.Position = data.m_position00;
                GetLaneDirection(data.m_position00, out startPosA.Direction, out startPosA.LanesUsed);
                startPosA.ComparisonValue = 0f;
            }
            else
            {
                _startLaneA = 0u;
                _startOffsetA = 0;
                startPosA = default(BufferItem);
            }
            BufferItem startPosB;
            if (data.m_position02.m_segment != 0 && num >= 3)
            {
                _startLaneB = PathManager.GetLaneID(data.m_position02);
                _startOffsetB = data.m_position02.m_offset;
                startPosB.LaneId = _startLaneB;
                startPosB.Position = data.m_position02;
                GetLaneDirection(data.m_position02, out startPosB.Direction, out startPosB.LanesUsed);
                startPosB.ComparisonValue = 0f;
            }
            else
            {
                _startLaneB = 0u;
                _startOffsetB = 0;
                startPosB = default(BufferItem);
            }
            BufferItem endPosA;
            if (data.m_position01.m_segment != 0 && num >= 2)
            {
                _endLaneA = PathManager.GetLaneID(data.m_position01);
                endPosA.LaneId = _endLaneA;
                endPosA.Position = data.m_position01;
                GetLaneDirection(data.m_position01, out endPosA.Direction, out endPosA.LanesUsed);
                endPosA.MethodDistance = 0f;
                endPosA.ComparisonValue = 0f;
            }
            else
            {
                _endLaneA = 0u;
                endPosA = default(BufferItem);
            }
            BufferItem endPosB;
            if (data.m_position03.m_segment != 0 && num >= 4)
            {
                _endLaneB = PathManager.GetLaneID(data.m_position03);
                endPosB.LaneId = _endLaneB;
                endPosB.Position = data.m_position03;
                GetLaneDirection(data.m_position03, out endPosB.Direction, out endPosB.LanesUsed);
                endPosB.MethodDistance = 0f;
                endPosB.ComparisonValue = 0f;
            }
            else
            {
                _endLaneB = 0u;
                endPosB = default(BufferItem);
            }
            if (data.m_position11.m_segment != 0 && num2 >= 1)
            {
                _vehicleLane = PathManager.GetLaneID(data.m_position11);
                _vehicleOffset = data.m_position11.m_offset;
            }
            else
            {
                _vehicleLane = 0u;
                _vehicleOffset = 0;
            }
            BufferItem goalItem = default(BufferItem);
            byte b = 0;
            _bufferMinPos = 0;
            _bufferMaxPos = -1;
            if (_pathFindIndex == 0u)
            {
                uint num3 = 4294901760u;
                for (int i = 0; i < 262144; i++)
                {
                    _laneLocation[i] = num3;
                }
            }
            for (int j = 0; j < 1024; j++)
            {
                _bufferMin[j] = 0;
                _bufferMax[j] = -1;
            }
            if (endPosA.Position.m_segment != 0)
            {
                _bufferMax[0]++;
                _buffer[++_bufferMaxPos] = endPosA;
            }
            if (endPosB.Position.m_segment != 0)
            {
                _bufferMax[0]++;
                _buffer[++_bufferMaxPos] = endPosB;
            }
            bool flag = false;
            while (_bufferMinPos <= _bufferMaxPos)
            {
                int num4 = _bufferMin[_bufferMinPos];
                int num5 = _bufferMax[_bufferMinPos];
                if (num4 > num5)
                {
                    _bufferMinPos++;
                }
                else
                {
                    _bufferMin[_bufferMinPos] = num4 + 1;
                    BufferItem currentItem = _buffer[(_bufferMinPos << 6) + num4];
                    if (currentItem.Position.m_segment == startPosA.Position.m_segment && currentItem.Position.m_lane == startPosA.Position.m_lane)
                    {
                        if ((byte)(currentItem.Direction & NetInfo.Direction.Forward) != 0 && currentItem.Position.m_offset >= _startOffsetA)
                        {
                            goalItem = currentItem;
                            b = _startOffsetA;
                            flag = true;
                            break;
                        }
                        if ((byte)(currentItem.Direction & NetInfo.Direction.Backward) != 0 && currentItem.Position.m_offset <= _startOffsetA)
                        {
                            goalItem = currentItem;
                            b = _startOffsetA;
                            flag = true;
                            break;
                        }
                    }
                    if (currentItem.Position.m_segment == startPosB.Position.m_segment && currentItem.Position.m_lane == startPosB.Position.m_lane)
                    {
                        if ((byte)(currentItem.Direction & NetInfo.Direction.Forward) != 0 && currentItem.Position.m_offset >= _startOffsetB)
                        {
                            goalItem = currentItem;
                            b = _startOffsetB;
                            flag = true;
                            break;
                        }
                        if ((byte)(currentItem.Direction & NetInfo.Direction.Backward) != 0 && currentItem.Position.m_offset <= _startOffsetB)
                        {
                            goalItem = currentItem;
                            b = _startOffsetB;
                            flag = true;
                            break;
                        }
                    }
                    if ((byte)(currentItem.Direction & NetInfo.Direction.Forward) != 0)
                    {
                        ushort startNode = instance.m_segments.m_buffer[currentItem.Position.m_segment].m_startNode;
                        ProcessItem1(currentItem, startNode, ref instance.m_nodes.m_buffer[startNode], 0, false, ref data);
                    }
                    if ((byte)(currentItem.Direction & NetInfo.Direction.Backward) != 0)
                    {
                        ushort endNode = instance.m_segments.m_buffer[currentItem.Position.m_segment].m_endNode;
                        ProcessItem1(currentItem, endNode, ref instance.m_nodes.m_buffer[endNode], 255, false, ref data);
                    }
                    int num6 = 0;
                    ushort num7 = instance.m_lanes.m_buffer[(int)((UIntPtr)currentItem.LaneId)].m_nodes;
                    if (num7 != 0)
                    {
                        ushort startNode2 = instance.m_segments.m_buffer[currentItem.Position.m_segment].m_startNode;
                        ushort endNode2 = instance.m_segments.m_buffer[currentItem.Position.m_segment].m_endNode;
                        bool flag2 = ((instance.m_nodes.m_buffer[startNode2].m_flags | instance.m_nodes.m_buffer[endNode2].m_flags) & NetNode.Flags.Disabled) != NetNode.Flags.None;
                        while (num7 != 0)
                        {
                            NetInfo.Direction direction = NetInfo.Direction.None;
                            byte laneOffset = instance.m_nodes.m_buffer[num7].m_laneOffset;
                            if (laneOffset <= currentItem.Position.m_offset)
                            {
                                direction |= NetInfo.Direction.Forward;
                            }
                            if (laneOffset >= currentItem.Position.m_offset)
                            {
                                direction |= NetInfo.Direction.Backward;
                            }
                            if ((byte)(currentItem.Direction & direction) != 0 && (!flag2 || (instance.m_nodes.m_buffer[num7].m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None))
                            {
                                ProcessItem1(currentItem, num7, ref instance.m_nodes.m_buffer[num7], laneOffset, true, ref data);
                            }
                            num7 = instance.m_nodes.m_buffer[num7].m_nextLaneNode;
                            if (++num6 == 32768)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            if (!flag)
            {
                var expr_8D5Cp0 = PathUnits.m_buffer;
                var expr_8D5Cp1 = (UIntPtr)unit;
                expr_8D5Cp0[(int)expr_8D5Cp1].m_pathFindFlags = (byte)(expr_8D5Cp0[(int)expr_8D5Cp1].m_pathFindFlags | 8);
                return;
            }
            var num8 = goalItem.ComparisonValue * _maxLength;
            PathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = num8;
            var num9 = unit;
            var num10 = 0;
            var num11 = 0;
            var position = goalItem.Position;
            if ((position.m_segment != endPosA.Position.m_segment || position.m_lane != endPosA.Position.m_lane || position.m_offset != endPosA.Position.m_offset) && (position.m_segment != endPosB.Position.m_segment || position.m_lane != endPosB.Position.m_lane || position.m_offset != endPosB.Position.m_offset))
            {
                if (b != position.m_offset)
                {
                    var position2 = position;
                    position2.m_offset = b;
                    PathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position2);
                }
                PathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position);
                position = _laneTarget[(int)((UIntPtr)goalItem.LaneId)];
            }
            for (var k = 0; k < 262144; k++)
            {
                PathUnits.m_buffer[(int)((UIntPtr)num9)].SetPosition(num10++, position);
                if ((position.m_segment == endPosA.Position.m_segment && position.m_lane == endPosA.Position.m_lane && position.m_offset == endPosA.Position.m_offset) || (position.m_segment == endPosB.Position.m_segment && position.m_lane == endPosB.Position.m_lane && position.m_offset == endPosB.Position.m_offset))
                {
                    PathUnits.m_buffer[(int)((UIntPtr)num9)].m_positionCount = (byte)num10;
                    num11 += num10;
                    if (num11 != 0)
                    {
                        num9 = PathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit;
                        num10 = PathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount;
                        var num12 = 0;
                        while (num9 != 0u)
                        {
                            PathUnits.m_buffer[(int)((UIntPtr)num9)].m_length = num8 * (num11 - num10) / num11;
                            num10 += PathUnits.m_buffer[(int)((UIntPtr)num9)].m_positionCount;
                            num9 = PathUnits.m_buffer[(int)((UIntPtr)num9)].m_nextPathUnit;
                            if (++num12 >= 262144)
                            {
                                CODebugBase<LogChannel>.Error(LogChannel.Core, "Invalid list detected!\n" + Environment.StackTrace);
                                break;
                            }
                        }
                    }
                    var exprBe2Cp0 = PathUnits.m_buffer;
                    var exprBe2Cp1 = (UIntPtr)unit;
                    exprBe2Cp0[(int)exprBe2Cp1].m_pathFindFlags = (byte)(exprBe2Cp0[(int)exprBe2Cp1].m_pathFindFlags | 4);
                    return;
                }
                if (num10 == 12)
                {
                    while (!Monitor.TryEnter(_bufferLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                    {
                    }
                    uint num13;
                    try
                    {
                        var localRandom = _pathRandomizer;
                        if (!PathUnits.CreateItem(out num13, ref localRandom))
                        {
                            var exprCe1Cp0 = PathUnits.m_buffer;
                            var exprCe1Cp1 = (UIntPtr)unit;
                            exprCe1Cp0[(int)exprCe1Cp1].m_pathFindFlags = (byte)(exprCe1Cp0[(int)exprCe1Cp1].m_pathFindFlags | 8);
                            return;
                        }
                        _pathRandomizer = localRandom;
                        PathUnits.m_buffer[(int)((UIntPtr)num13)] = PathUnits.m_buffer[(int)((UIntPtr)num9)];
                        PathUnits.m_buffer[(int)((UIntPtr)num13)].m_referenceCount = 1;
                        PathUnits.m_buffer[(int)((UIntPtr)num13)].m_pathFindFlags = 4;
                        PathUnits.m_buffer[(int)((UIntPtr)num9)].m_nextPathUnit = num13;
                        PathUnits.m_buffer[(int)((UIntPtr)num9)].m_positionCount = (byte)num10;
                        num11 += num10;
                        Singleton<PathManager>.instance.m_pathUnitCount = (int)(PathUnits.ItemCount() - 1u);
                    }
                    finally
                    {
                        Monitor.Exit(_bufferLock);
                    }
                    num9 = num13;
                    num10 = 0;
                }
                var laneId = PathManager.GetLaneID(position);
                position = _laneTarget[(int)((UIntPtr)laneId)];
            }
            var exprD65Cp0 = PathUnits.m_buffer;
            var exprD65Cp1 = (UIntPtr)unit;
            exprD65Cp0[(int)exprD65Cp1].m_pathFindFlags = (byte)(exprD65Cp0[(int)exprD65Cp1].m_pathFindFlags | 8);
        }

        private void ProcessItem1(BufferItem item, ushort nodeId, ref NetNode node, byte connectOffset, bool isMiddle, ref PathUnit data)
        {
            var instance = Singleton<NetManager>.instance;
            var flag = false;
            var num = 0;
            var info = instance.m_segments.m_buffer[item.Position.m_segment].Info;
            if (item.Position.m_lane < info.m_lanes.Length)
            {
                var lane = info.m_lanes[item.Position.m_lane];
                flag = (lane.m_laneType == NetInfo.LaneType.Pedestrian);
                if ((byte)(lane.m_finalDirection & NetInfo.Direction.Forward) != 0)
                {
                    num = lane.m_similarLaneIndex;
                }
                else
                {
                    num = lane.m_similarLaneCount - lane.m_similarLaneIndex - 1;
                }
            }
            if (isMiddle)
            {
                for (var j = 0; j < 8; j++)
                {
                    var segmentId = node.GetSegment(j);
                    if (segmentId != 0)
                    {
                        ProcessItem4(item, nodeId, segmentId, ref instance.m_segments.m_buffer[segmentId],
                        ref num, connectOffset, !flag, flag);
                    }
                }
            }
            else if (flag)
            {
                ushort segment2 = item.Position.m_segment;
                int lane2 = item.Position.m_lane;
                if (node.Info.m_class.m_service != ItemClass.Service.Beautification)
                {
                    bool flag2 = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.Bend | NetNode.Flags.Junction)) != NetNode.Flags.None;
                    int laneIndex;
                    int laneIndex2;
                    uint num2;
                    uint num3;
                    instance.m_segments.m_buffer[segment2].GetLeftAndRightLanes(nodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, lane2, out laneIndex, out laneIndex2, out num2, out num3);
                    ushort num4 = segment2;
                    ushort num5 = segment2;
                    if (num2 == 0u || num3 == 0u)
                    {
                        ushort leftSegment;
                        ushort rightSegment;
                        instance.m_segments.m_buffer[segment2].GetLeftAndRightSegments(nodeId, out leftSegment, out rightSegment);
                        int num6 = 0;
                        while (leftSegment != 0 && leftSegment != segment2 && num2 == 0u)
                        {
                            int num7;
                            int num8;
                            uint num9;
                            uint num10;
                            instance.m_segments.m_buffer[leftSegment].GetLeftAndRightLanes(nodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out num7, out num8, out num9, out num10);
                            if (num10 != 0u)
                            {
                                num4 = leftSegment;
                                laneIndex = num8;
                                num2 = num10;
                            }
                            else
                            {
                                leftSegment = instance.m_segments.m_buffer[leftSegment].GetLeftSegment(nodeId);
                            }
                            if (++num6 == 8)
                            {
                                break;
                            }
                        }
                        num6 = 0;
                        while (rightSegment != 0 && rightSegment != segment2 && num3 == 0u)
                        {
                            int num11;
                            int num12;
                            uint num13;
                            uint num14;
                            instance.m_segments.m_buffer[rightSegment].GetLeftAndRightLanes(nodeId, NetInfo.LaneType.Pedestrian, VehicleInfo.VehicleType.None, -1, out num11, out num12, out num13, out num14);
                            if (num13 != 0u)
                            {
                                num5 = rightSegment;
                                laneIndex2 = num11;
                                num3 = num13;
                            }
                            else
                            {
                                rightSegment = instance.m_segments.m_buffer[rightSegment].GetRightSegment(nodeId);
                            }
                            if (++num6 == 8)
                            {
                                break;
                            }
                        }
                    }
                    if (num2 != 0u && (num4 != segment2 || flag2))
                    {
                        ProcessItem5(item, nodeId, num4, ref instance.m_segments.m_buffer[num4], connectOffset, laneIndex, num2);
                    }
                    if (num3 != 0u && num3 != num2 && (num5 != segment2 || flag2))
                    {
                        ProcessItem5(item, nodeId, num5, ref instance.m_segments.m_buffer[num5], connectOffset, laneIndex2, num3);
                    }
                }
                else
                {
                    for (int j = 0; j < 8; j++)
                    {
                        ushort segment3 = node.GetSegment(j);
                        if (segment3 != 0 && segment3 != segment2)
                        {
                            ProcessItem4(item, nodeId, segment3, ref instance.m_segments.m_buffer[segment3], ref num, connectOffset, false, true);
                        }
                    }
                }
                NetInfo.LaneType laneType = _laneTypes & ~NetInfo.LaneType.Pedestrian;
                laneType &= ~(item.LanesUsed & NetInfo.LaneType.Vehicle);
                int num15;
                uint lane3;
                if (laneType != NetInfo.LaneType.None && instance.m_segments.m_buffer[segment2].GetClosestLane(lane2, laneType, _vehicleTypes, out num15, out lane3))
                {
                    NetInfo.Lane lane4 = info.m_lanes[num15];
                    byte connectOffset2;
                    if ((instance.m_segments.m_buffer[segment2].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None == ((byte)(lane4.m_finalDirection & NetInfo.Direction.Backward) != 0))
                    {
                        connectOffset2 = 1;
                    }
                    else
                    {
                        connectOffset2 = 254;
                    }
                    ProcessItem5(item, nodeId, segment2, ref instance.m_segments.m_buffer[segment2], connectOffset2, num15, lane3);
                }
            }
            else
            {
                bool flag3 = (node.m_flags & (NetNode.Flags.End | NetNode.Flags.OneWayOut)) != NetNode.Flags.None;
                bool flag4 = (byte)(_laneTypes & NetInfo.LaneType.Pedestrian) != 0;
                byte connectOffset3 = 0;
                if (flag4)
                {
                    if (_vehicleLane != 0u)
                    {
                        if (_vehicleLane != item.LaneId)
                        {
                            flag4 = false;
                        }
                        else
                        {
                            connectOffset3 = _vehicleOffset;
                        }
                    }
                    else if (_stablePath)
                    {
                        connectOffset3 = 128;
                    }
                    else
                    {
                        connectOffset3 = (byte)_pathRandomizer.UInt32(1u, 254u);
                    }
                }

                ushort num16 = instance.m_segments.m_buffer[item.Position.m_segment].GetRightSegment(nodeId);
                for (int k = 0; k < 8; k++)
                {
                    if (num16 == 0 || num16 == item.Position.m_segment)
                    {
                        break;
                    }

                    if (TrafficPriority.IsPrioritySegment(nodeId, num16) && data.m_position00.m_segment != num16)
                    {
                        var segment = instance.m_segments.m_buffer[num16];

                        var info2 = segment.Info;

                        uint segmentLanes = segment.m_lanes;
                        int infoLanes = 0;

                        var lanes = 0;

                        int[] laneNums = new int[16];
                        uint[] laneIds = new uint[16];

                        NetInfo.Direction dir = NetInfo.Direction.Forward;
                        if (segment.m_startNode == nodeId)
                            dir = NetInfo.Direction.Backward;
                        var dir2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? dir : NetInfo.InvertDirection(dir);
                        var dir3 = TrafficPriority.LeftHandDrive ? NetInfo.InvertDirection(dir2) : dir2;

                        var lanes1 = info2.m_lanes;

                        if (TrafficPriority.LeftHandDrive)
                        {
                            lanes1 = lanes1.Reverse().ToArray();
                        }

                        var laneArrows = 0;

                        while (infoLanes < lanes1.Length && segmentLanes != 0u)
                        {
                            if (((NetLane.Flags)instance.m_lanes.m_buffer[segmentLanes].m_flags & NetLane.Flags.Left) ==
                                NetLane.Flags.Left || ((NetLane.Flags)instance.m_lanes.m_buffer[segmentLanes].m_flags & NetLane.Flags.Right) ==
                                        NetLane.Flags.Right || ((NetLane.Flags)instance.m_lanes.m_buffer[segmentLanes].m_flags & NetLane.Flags.Forward) ==
                                        NetLane.Flags.Forward)
                            {
                                laneArrows++;
                            }

                            if (lanes1[infoLanes].m_laneType == NetInfo.LaneType.Vehicle && lanes1[infoLanes].m_direction == dir3)
                            {
                                if (TrafficPriority.IsLeftSegment(num16, item.Position.m_segment, nodeId))
                                {
                                    if (((NetLane.Flags)instance.m_lanes.m_buffer[segmentLanes].m_flags & NetLane.Flags.Left) ==
                                        NetLane.Flags.Left)
                                    {
                                        laneNums[lanes] = infoLanes;
                                        laneIds[lanes] = segmentLanes;
                                        lanes++;
                                    }
                                }
                                else if (TrafficPriority.IsRightSegment(num16, item.Position.m_segment, nodeId))
                                {
                                    if (((NetLane.Flags)instance.m_lanes.m_buffer[segmentLanes].m_flags & NetLane.Flags.Right) ==
                                        NetLane.Flags.Right)
                                    {
                                        laneNums[lanes] = infoLanes;
                                        laneIds[lanes] = segmentLanes;
                                        lanes++;
                                    }
                                }
                                else
                                {
                                    if (((NetLane.Flags)instance.m_lanes.m_buffer[segmentLanes].m_flags & NetLane.Flags.Forward) ==
                                        NetLane.Flags.Forward)
                                    {
                                        laneNums[lanes] = infoLanes;
                                        laneIds[lanes] = segmentLanes;
                                        lanes++;
                                    }
                                }
                            }

                            segmentLanes = instance.m_lanes.m_buffer[(int)((UIntPtr)segmentLanes)].m_nextLane;
                            infoLanes++;
                        }

                        if (laneArrows > 0)
                        {
                            var newLaneNum = 0;
                            var newLaneId = 0u;
                            var newLane = -1;

                            if (lanes > 0)
                            {
                                if (lanes == 1)
                                {
                                    newLaneNum = laneNums[0];
                                    newLaneId = laneIds[0];
                                }
                                else
                                {
                                    var laneFound = false;

                                    if (info2.m_lanes.Length == info.m_lanes.Length)
                                    {

                                        for (var i = 0; i < laneNums.Length; i++)
                                        {
                                            if (laneNums[i] == item.Position.m_lane)
                                            {
                                                newLaneNum = laneNums[i];
                                                newLaneId = laneIds[i];
                                                laneFound = true;
                                                break;
                                            }
                                        }
                                    }

                                    if (!laneFound)
                                    {
                                        var lanePos = Mathf.Abs(info.m_lanes[item.Position.m_lane].m_position);
                                        var closest = 100f;
                                        for (var i = 0; i < lanes; i++)
                                        {
                                            var newLanePos = Mathf.Abs(info2.m_lanes[laneNums[i]].m_position);

                                            if (Math.Abs(newLanePos - lanePos) < closest)
                                            {
                                                closest = Mathf.Abs(newLanePos - lanePos);
                                                newLane = i;
                                            }
                                        }

                                        if (newLane == -1)
                                        {
                                            newLaneNum = laneNums[0];
                                            newLaneId = laneIds[0];
                                        }
                                        else
                                        {
                                            newLaneNum = laneNums[newLane];
                                            newLaneId = laneIds[newLane];
                                        }
                                    }
                                }

                                ProcessItem2(item, nodeId, num16, ref instance.m_segments.m_buffer[num16],
                                    ref num,
                                    connectOffset, true, false, newLaneId, newLaneNum);
                            }
                        }
                        else
                        {
                            if (ProcessItem4(item, nodeId, num16, ref instance.m_segments.m_buffer[num16], ref num,
                                connectOffset, true, false))
                            {
                                flag3 = true;
                            }
                        }
                    }
                    //else if (TrafficRoadRestrictions.isSegment(num16))
                    //{
                    //    var restSegment = TrafficRoadRestrictions.getSegment(num16);

                    //    var preferedLaneAllows = 100;
                    //    uint preferedLaneId = 0;
                    //    int preferedLaneNum = 0;

                    //    NetInfo.Lane lane = info.m_lanes[(int)item.Position.m_lane];

                    //    for (var i = 0; i < restSegment.lanes.Count; i++)
                    //    {
                    //        if ((byte) (lane.m_finalDirection & NetInfo.Direction.Forward) != 0)
                    //        {
                    //            if ((byte) (restSegment.lanes[i].direction & NetInfo.Direction.Forward) == 0)
                    //            {
                    //                continue;
                    //            }
                    //        }
                    //        else
                    //        {
                    //            if ((byte) (restSegment.lanes[i].direction & NetInfo.Direction.Backward) == 0)
                    //            {
                    //                continue;
                    //            }
                    //        }

                    //        if (this._vehicleType == TrafficRoadRestrictions.VehicleTypes.Car)
                    //        {
                    //            if (restSegment.lanes[i].enableCars)
                    //            {
                    //                if (restSegment.lanes[i].laneNum == num)
                    //                {
                    //                    preferedLaneId = restSegment.lanes[i].laneID;
                    //                    preferedLaneNum = restSegment.lanes[i].laneNum;
                    //                    break;
                    //                }
                    //                else if (restSegment.lanes[i].enabledTypes < preferedLaneAllows)
                    //                {
                    //                    preferedLaneId = restSegment.lanes[i].laneID;
                    //                    preferedLaneNum = restSegment.lanes[i].laneNum;
                    //                    preferedLaneAllows = restSegment.lanes[i].enabledTypes;
                    //                }
                    //            }
                    //        }
                    //        else if (this._vehicleType == TrafficRoadRestrictions.VehicleTypes.Service)
                    //        {
                    //            if (restSegment.lanes[i].enableService)
                    //            {
                    //                if (restSegment.lanes[i].laneNum == num)
                    //                {
                    //                    preferedLaneId = restSegment.lanes[i].laneID;
                    //                    preferedLaneNum = restSegment.lanes[i].laneNum;
                    //                    break;
                    //                }
                    //                else if (restSegment.lanes[i].enabledTypes < preferedLaneAllows)
                    //                {
                    //                    preferedLaneId = restSegment.lanes[i].laneID;
                    //                    preferedLaneNum = restSegment.lanes[i].laneNum;
                    //                    preferedLaneAllows = restSegment.lanes[i].enabledTypes;
                    //                }
                    //            }
                    //        }
                    //        else if (this._vehicleType == TrafficRoadRestrictions.VehicleTypes.Cargo)
                    //        {
                    //            if (restSegment.lanes[i].enableCargo)
                    //            {
                    //                if (restSegment.lanes[i].laneNum == num)
                    //                {
                    //                    preferedLaneId = restSegment.lanes[i].laneID;
                    //                    preferedLaneNum = restSegment.lanes[i].laneNum;
                    //                    break;
                    //                }
                    //                else if (restSegment.lanes[i].enabledTypes < preferedLaneAllows)
                    //                {
                    //                    preferedLaneId = restSegment.lanes[i].laneID;
                    //                    preferedLaneNum = restSegment.lanes[i].laneNum;
                    //                    preferedLaneAllows = restSegment.lanes[i].enabledTypes;
                    //                }
                    //            }
                    //        }
                    //        else if (this._vehicleType == TrafficRoadRestrictions.VehicleTypes.Transport)
                    //        {
                    //            if (restSegment.lanes[i].enableTransport)
                    //            {
                    //                if (restSegment.lanes[i].laneNum == num)
                    //                {
                    //                    preferedLaneId = restSegment.lanes[i].laneID;
                    //                    preferedLaneNum = restSegment.lanes[i].laneNum;
                    //                    break;
                    //                }
                    //                else if (restSegment.lanes[i].enabledTypes < preferedLaneAllows)
                    //                {
                    //                    preferedLaneId = restSegment.lanes[i].laneID;
                    //                    preferedLaneNum = restSegment.lanes[i].laneNum;
                    //                    preferedLaneAllows = restSegment.lanes[i].enabledTypes;
                    //                }
                    //            }
                    //        }
                    //    }

                    //    if (preferedLaneId != 0)
                    //    {
                    //        this.ProcessItem2(item, nodeID, num16, ref instance.m_segments._buffer[(int)num16],
                    //            ref num,
                    //            connectOffset, true, false, preferedLaneId, preferedLaneNum);
                    //    }
                    //}
                    else
                    {
                        if (ProcessItem4(item, nodeId, num16, ref instance.m_segments.m_buffer[num16], ref num,
                            connectOffset, true, false))
                        {
                            flag3 = true;
                        }
                    }

                    num16 = instance.m_segments.m_buffer[num16].GetRightSegment(nodeId);
                }
                if (flag3)
                {
                    num16 = item.Position.m_segment;
                    ProcessItem4(item, nodeId, num16, ref instance.m_segments.m_buffer[num16], ref num, connectOffset, true, false);
                }
                int laneIndex3;
                uint lane5;
                if (flag4 && instance.m_segments.m_buffer[num16].GetClosestLane(item.Position.m_lane, NetInfo.LaneType.Pedestrian, _vehicleTypes, out laneIndex3, out lane5))
                {
                    ProcessItem5(item, nodeId, num16, ref instance.m_segments.m_buffer[num16], connectOffset3, laneIndex3, lane5);
                }
            }
            if (node.m_lane != 0u)
            {
                var targetDisabled = (node.m_flags & NetNode.Flags.Disabled) != NetNode.Flags.None;
                var segment4 = instance.m_lanes.m_buffer[(int)((UIntPtr)node.m_lane)].m_segment;
                if (segment4 != 0 && segment4 != item.Position.m_segment)
                {
                    ProcessItem3(item, targetDisabled, segment4, ref instance.m_segments.m_buffer[segment4], node.m_lane, node.m_laneOffset, connectOffset);
                }
            }
        }

        private void ProcessItem2(BufferItem item, ushort targetNode, ushort segmentId, ref NetSegment segment, ref int currentTargetIndex, byte connectOffset, bool enableVehicle, bool enablePedestrian, uint laneId, int laneNum)
        {
            if ((segment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None)
            {
                return;
            }
            var instance = Singleton<NetManager>.instance;
            var info = segment.Info;
            var info2 = instance.m_segments.m_buffer[item.Position.m_segment].Info;
            var num2 = laneId;
            var direction = (targetNode != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
            var direction2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? direction : NetInfo.InvertDirection(direction);
            var num3 = 0.01f - Mathf.Min(info.m_maxTurnAngleCos, info2.m_maxTurnAngleCos);
            if (num3 < 1f)
            {
                Vector3 vector;
                if (targetNode == instance.m_segments.m_buffer[item.Position.m_segment].m_startNode)
                {
                    vector = instance.m_segments.m_buffer[item.Position.m_segment].m_startDirection;
                }
                else
                {
                    vector = instance.m_segments.m_buffer[item.Position.m_segment].m_endDirection;
                }
                Vector3 vector2;
                if ((byte)(direction & NetInfo.Direction.Forward) != 0)
                {
                    vector2 = segment.m_endDirection;
                }
                else
                {
                    vector2 = segment.m_startDirection;
                }
                float num4 = vector.x * vector2.x + vector.z * vector2.z;
                if (num4 >= num3)
                {
                    return;
                }
            }
            float num5 = 1f;
            float num6 = 1f;
            NetInfo.LaneType laneType = NetInfo.LaneType.None;
            VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
            if (item.Position.m_lane < info2.m_lanes.Length)
            {
                NetInfo.Lane lane = info2.m_lanes[item.Position.m_lane];
                laneType = lane.m_laneType;
                vehicleType = lane.m_vehicleType;

                if (TrafficRoadRestrictions.IsSegment(item.Position.m_segment))
                {
                    var restrictionSegment = TrafficRoadRestrictions.GetSegment(item.Position.m_segment);

                    if (restrictionSegment.SpeedLimits[item.Position.m_lane] > 0.1f)
                    {
                        num5 = restrictionSegment.SpeedLimits[item.Position.m_lane];
                    }
                    else
                    {
                        num5 = lane.m_speedLimit;
                    }
                }
                else
                {
                    num5 = lane.m_speedLimit;
                }
                num6 = CalculateLaneSpeed(connectOffset, item.Position.m_offset, ref instance.m_segments.m_buffer[item.Position.m_segment], lane);
            }
            float num7 = instance.m_segments.m_buffer[item.Position.m_segment].m_averageLength;
            if (!_stablePath)
            {
                Randomizer randomizer = new Randomizer(_pathFindIndex << 16 | item.Position.m_segment);
                num7 *= (randomizer.Int32(900, 1000 + instance.m_segments.m_buffer[item.Position.m_segment].m_trafficDensity * 10) + _pathRandomizer.Int32(20u)) * 0.001f;
            }
            if (_isHeavyVehicle && (instance.m_segments.m_buffer[item.Position.m_segment].m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None)
            {
                num7 *= 10f;
            }
            float num8 = Mathf.Abs(connectOffset - item.Position.m_offset) * 0.003921569f * num7;
            float num9 = item.MethodDistance + num8;
            float num10 = item.ComparisonValue + num8 / (num6 * _maxLength);
            Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.LaneId)].CalculatePosition(connectOffset * 0.003921569f);
            int num11 = laneNum;
            bool flag = (instance.m_nodes.m_buffer[targetNode].m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
            NetInfo.LaneType laneType2 = _laneTypes;
            if (!enableVehicle)
            {
                laneType2 &= ~NetInfo.LaneType.Vehicle;
            }
            if (!enablePedestrian)
            {
                laneType2 &= ~NetInfo.LaneType.Pedestrian;
            }
            int num12 = laneNum;

            NetInfo.Lane lane2 = info.m_lanes[num12];

            if (TrafficRoadRestrictions.IsSegment(instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_segment))
            {
                var restrictionSegment = TrafficRoadRestrictions.GetSegment(instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_segment);

                if (restrictionSegment.SpeedLimits[item.Position.m_lane] > 0.1f)
                {
                }
            }

            if (lane2.CheckType(laneType2, _vehicleTypes) && (segmentId != item.Position.m_segment || num12 != item.Position.m_lane) && (byte)(lane2.m_finalDirection & direction2) != 0)
            {
                Vector3 a;
                if ((byte)(direction & NetInfo.Direction.Forward) != 0)
                {
                    a = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_bezier.d;
                }
                else
                {
                    a = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_bezier.a;
                }
                var num13 = Vector3.Distance(a, b);
                if (flag)
                {
                    num13 *= 2f;
                }
                var num14 = num13 / ((num5 + lane2.m_speedLimit) * 0.5f * _maxLength);
                BufferItem item2;
                item2.Position.m_segment = segmentId;
                item2.Position.m_lane = (byte)laneNum;
                item2.Position.m_offset = (byte)(((byte)(direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
                if (laneType != lane2.m_laneType)
                {
                    item2.MethodDistance = 0f;
                }
                else
                {
                    item2.MethodDistance = num9 + num13;
                }

                item2.ComparisonValue = num10 + num14;
                if (num2 == _startLaneA)
                {
                    float num15 = CalculateLaneSpeed(_startOffsetA, item2.Position.m_offset, ref segment, lane2);
                    float num16 = Mathf.Abs(item2.Position.m_offset - _startOffsetA) * 0.003921569f;
                    item2.ComparisonValue += num16 * segment.m_averageLength / (num15 * _maxLength);
                }
                if (num2 == _startLaneB)
                {
                    float num17 = CalculateLaneSpeed(_startOffsetB, item2.Position.m_offset, ref segment, lane2);
                    float num18 = Mathf.Abs(item2.Position.m_offset - _startOffsetB) * 0.003921569f;
                    item2.ComparisonValue += num18 * segment.m_averageLength / (num17 * _maxLength);
                }
                if (!_ignoreBlocked && (segment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && lane2.m_laneType == NetInfo.LaneType.Vehicle)
                {
                    item2.ComparisonValue += 0.1f;
                }
                item2.Direction = direction;
                item2.LanesUsed = (item.LanesUsed | lane2.m_laneType);
                item2.LaneId = laneId;
                if (lane2.m_laneType == laneType && lane2.m_vehicleType == vehicleType)
                {
                    int firstTarget = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_firstTarget;
                    int lastTarget = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_lastTarget;
                    if (currentTargetIndex < firstTarget || currentTargetIndex >= lastTarget)
                    {
                        item2.ComparisonValue += Mathf.Max(1f, num13 * 3f - 3f) / ((num5 + lane2.m_speedLimit) * 0.5f * _maxLength);
                    }
                }

                AddBufferItem(item2, item.Position);
            }

            currentTargetIndex = num11;
        }

        protected virtual NetLane.Flags GetLaneFlags(ushort segmentId, ushort nodeId)
        {
            NetManager instance = NetManager.instance;
            NetSegment seg = instance.m_segments.m_buffer[segmentId];
            NetLane.Flags flags = NetLane.Flags.None;
            NetInfo.Direction dir = NetInfo.Direction.Forward;
            if (seg.m_startNode == nodeId)
                dir = NetInfo.Direction.Backward;
            ulong currentLane = seg.m_lanes;
            for (int i = 0; i < seg.Info.m_lanes.Length; i++)
            {
                if (((seg.Info.m_lanes[i].m_direction & dir) == dir) && seg.Info.m_lanes[i].m_laneType == NetInfo.LaneType.Vehicle)
                    flags |= (NetLane.Flags)instance.m_lanes.m_buffer[currentLane].m_flags;
                currentLane = instance.m_lanes.m_buffer[currentLane].m_nextLane;
            }
            return flags;
        }



        private static float CalculateLaneSpeed(byte startOffset, byte endOffset, ref NetSegment segment, NetInfo.Lane laneInfo)
        {
            var direction = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? laneInfo.m_finalDirection : NetInfo.InvertDirection(laneInfo.m_finalDirection);
            if ((byte)(direction & NetInfo.Direction.Avoid) == 0)
            {
                return laneInfo.m_speedLimit;
            }
            if (endOffset > startOffset && direction == NetInfo.Direction.AvoidForward)
            {
                return laneInfo.m_speedLimit * 0.1f;
            }
            if (endOffset < startOffset && direction == NetInfo.Direction.AvoidBackward)
            {
                return laneInfo.m_speedLimit * 0.1f;
            }
            return laneInfo.m_speedLimit * 0.2f;
        }

        private void ProcessItem3(BufferItem item, bool targetDisabled, ushort segmentId, ref NetSegment segment, uint lane, byte offset, byte connectOffset)
        {
            if ((segment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None)
            {
                return;
            }
            NetManager instance = Singleton<NetManager>.instance;
            if (targetDisabled && ((instance.m_nodes.m_buffer[segment.m_startNode].m_flags | instance.m_nodes.m_buffer[segment.m_endNode].m_flags) & NetNode.Flags.Disabled) == NetNode.Flags.None)
            {
                return;
            }
            NetInfo info = segment.Info;
            NetInfo info2 = instance.m_segments.m_buffer[item.Position.m_segment].Info;
            int num = info.m_lanes.Length;
            uint num2 = segment.m_lanes;
            float num3 = 1f;
            float num4 = 1f;
            NetInfo.LaneType laneType = NetInfo.LaneType.None;
            if (item.Position.m_lane < info2.m_lanes.Length)
            {
                NetInfo.Lane lane2 = info2.m_lanes[item.Position.m_lane];
                num3 = lane2.m_speedLimit;
                laneType = lane2.m_laneType;
                num4 = CalculateLaneSpeed(connectOffset, item.Position.m_offset, ref instance.m_segments.m_buffer[item.Position.m_segment], lane2);
            }
            float averageLength = instance.m_segments.m_buffer[item.Position.m_segment].m_averageLength;
            float num5 = Mathf.Abs(connectOffset - item.Position.m_offset) * 0.003921569f * averageLength;
            float num6 = item.MethodDistance + num5;
            float num7 = item.ComparisonValue + num5 / (num4 * _maxLength);
            Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.LaneId)].CalculatePosition(connectOffset * 0.003921569f);
            int num8 = 0;
            while (num8 < num && num2 != 0u)
            {
                if (lane == num2)
                {
                    NetInfo.Lane lane3 = info.m_lanes[num8];
                    if (lane3.CheckType(_laneTypes, _vehicleTypes))
                    {
                        Vector3 a = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].CalculatePosition(offset * 0.003921569f);
                        float num9 = Vector3.Distance(a, b);
                        BufferItem item2;
                        item2.Position.m_segment = segmentId;
                        item2.Position.m_lane = (byte)num8;
                        item2.Position.m_offset = offset;
                        if (laneType != lane3.m_laneType)
                        {
                            item2.MethodDistance = 0f;
                        }
                        else
                        {
                            item2.MethodDistance = num6 + num9;
                        }
                        if (lane3.m_laneType != NetInfo.LaneType.Pedestrian || item2.MethodDistance < 1000f)
                        {
                            item2.ComparisonValue = num7 + num9 / ((num3 + lane3.m_speedLimit) * 0.5f * _maxLength);
                            if (lane == _startLaneA)
                            {
                                float num10 = CalculateLaneSpeed(_startOffsetA, item2.Position.m_offset, ref segment, lane3);
                                float num11 = Mathf.Abs(item2.Position.m_offset - _startOffsetA) * 0.003921569f;
                                item2.ComparisonValue += num11 * segment.m_averageLength / (num10 * _maxLength);
                            }
                            if (lane == _startLaneB)
                            {
                                float num12 = CalculateLaneSpeed(_startOffsetB, item2.Position.m_offset, ref segment, lane3);
                                float num13 = Mathf.Abs(item2.Position.m_offset - _startOffsetB) * 0.003921569f;
                                item2.ComparisonValue += num13 * segment.m_averageLength / (num12 * _maxLength);
                            }
                            if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                            {
                                item2.Direction = NetInfo.InvertDirection(lane3.m_finalDirection);
                            }
                            else
                            {
                                item2.Direction = lane3.m_finalDirection;
                            }
                            item2.LaneId = lane;
                            item2.LanesUsed = (item.LanesUsed | lane3.m_laneType);
                            AddBufferItem(item2, item.Position);
                        }
                    }
                    return;
                }
                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num8++;
            }
        }

        private bool ProcessItem4(BufferItem item, ushort targetNode, ushort segmentId, ref NetSegment segment, ref int currentTargetIndex, byte connectOffset, bool enableVehicle, bool enablePedestrian)
        {
            var result = false;
            if ((segment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None)
            {
                return false;
            }
            NetManager instance = Singleton<NetManager>.instance;
            NetInfo info = segment.Info;
            NetInfo info2 = instance.m_segments.m_buffer[item.Position.m_segment].Info;
            int num = info.m_lanes.Length;
            uint num2 = segment.m_lanes;
            NetInfo.Direction direction = (targetNode != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
            NetInfo.Direction direction2 = ((segment.m_flags & NetSegment.Flags.Invert) == NetSegment.Flags.None) ? direction : NetInfo.InvertDirection(direction);
            float num3 = 0.01f - Mathf.Min(info.m_maxTurnAngleCos, info2.m_maxTurnAngleCos);
            if (num3 < 1f)
            {
                Vector3 vector;
                if (targetNode == instance.m_segments.m_buffer[item.Position.m_segment].m_startNode)
                {
                    vector = instance.m_segments.m_buffer[item.Position.m_segment].m_startDirection;
                }
                else
                {
                    vector = instance.m_segments.m_buffer[item.Position.m_segment].m_endDirection;
                }
                Vector3 vector2;
                if ((byte)(direction & NetInfo.Direction.Forward) != 0)
                {
                    vector2 = segment.m_endDirection;
                }
                else
                {
                    vector2 = segment.m_startDirection;
                }
                float num4 = vector.x * vector2.x + vector.z * vector2.z;
                if (num4 >= num3)
                {
                    return false;
                }
            }
            float num5 = 1f;
            float num6 = 1f;
            NetInfo.LaneType laneType = NetInfo.LaneType.None;
            VehicleInfo.VehicleType vehicleType = VehicleInfo.VehicleType.None;
            if (item.Position.m_lane < info2.m_lanes.Length)
            {
                NetInfo.Lane lane = info2.m_lanes[item.Position.m_lane];
                laneType = lane.m_laneType;
                vehicleType = lane.m_vehicleType;
                num5 = lane.m_speedLimit;
                num6 = CalculateLaneSpeed(connectOffset, item.Position.m_offset, ref instance.m_segments.m_buffer[item.Position.m_segment], lane);
            }
            float num7 = instance.m_segments.m_buffer[item.Position.m_segment].m_averageLength;
            if (!_stablePath)
            {
                Randomizer randomizer = new Randomizer(_pathFindIndex << 16 | item.Position.m_segment);
                num7 *= (randomizer.Int32(900, 1000 + instance.m_segments.m_buffer[item.Position.m_segment].m_trafficDensity * 10) + _pathRandomizer.Int32(20u)) * 0.001f;
            }
            if (_isHeavyVehicle && (instance.m_segments.m_buffer[item.Position.m_segment].m_flags & NetSegment.Flags.HeavyBan) != NetSegment.Flags.None)
            {
                num7 *= 10f;
            }
            float num8 = Mathf.Abs(connectOffset - item.Position.m_offset) * 0.003921569f * num7;
            float num9 = item.MethodDistance + num8;
            float num10 = item.ComparisonValue + num8 / (num6 * _maxLength);
            Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.LaneId)].CalculatePosition(connectOffset * 0.003921569f);
            int num11 = currentTargetIndex;
            bool flag = (instance.m_nodes.m_buffer[targetNode].m_flags & NetNode.Flags.Transition) != NetNode.Flags.None;
            NetInfo.LaneType laneType2 = _laneTypes;
            if (!enableVehicle)
            {
                laneType2 &= ~NetInfo.LaneType.Vehicle;
            }
            if (!enablePedestrian)
            {
                laneType2 &= ~NetInfo.LaneType.Pedestrian;
            }
            int num12 = 0;
            while (num12 < num && num2 != 0u)
            {
                NetInfo.Lane lane2 = info.m_lanes[num12];
                if ((byte)(lane2.m_finalDirection & direction2) != 0)
                {
                    if (lane2.CheckType(laneType2, _vehicleTypes) && (segmentId != item.Position.m_segment || num12 != item.Position.m_lane) && (byte)(lane2.m_finalDirection & direction2) != 0)
                    {
                        Vector3 a;
                        if ((byte)(direction & NetInfo.Direction.Forward) != 0)
                        {
                            a = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_bezier.d;
                        }
                        else
                        {
                            a = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_bezier.a;
                        }
                        float num13 = Vector3.Distance(a, b);
                        if (flag)
                        {
                            num13 *= 2f;
                        }
                        float num14 = num13 / ((num5 + lane2.m_speedLimit) * 0.5f * _maxLength);
                        BufferItem item2;
                        item2.Position.m_segment = segmentId;
                        item2.Position.m_lane = (byte)num12;
                        item2.Position.m_offset = (byte)(((byte)(direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
                        if (laneType != lane2.m_laneType)
                        {
                            item2.MethodDistance = 0f;
                        }
                        else
                        {
                            item2.MethodDistance = num9 + num13;
                        }
                        if (lane2.m_laneType != NetInfo.LaneType.Pedestrian || item2.MethodDistance < 1000f)
                        {
                            item2.ComparisonValue = num10 + num14;
                            if (num2 == _startLaneA)
                            {
                                float num15 = CalculateLaneSpeed(_startOffsetA, item2.Position.m_offset, ref segment, lane2);
                                float num16 = Mathf.Abs(item2.Position.m_offset - _startOffsetA) * 0.003921569f;
                                item2.ComparisonValue += num16 * segment.m_averageLength / (num15 * _maxLength);
                            }
                            if (num2 == _startLaneB)
                            {
                                float num17 = CalculateLaneSpeed(_startOffsetB, item2.Position.m_offset, ref segment, lane2);
                                float num18 = Mathf.Abs(item2.Position.m_offset - _startOffsetB) * 0.003921569f;
                                item2.ComparisonValue += num18 * segment.m_averageLength / (num17 * _maxLength);
                            }
                            if (!_ignoreBlocked && (segment.m_flags & NetSegment.Flags.Blocked) != NetSegment.Flags.None && lane2.m_laneType == NetInfo.LaneType.Vehicle)
                            {
                                item2.ComparisonValue += 0.1f;
                                result = true;
                            }
                            item2.Direction = direction;
                            item2.LanesUsed = (item.LanesUsed | lane2.m_laneType);
                            item2.LaneId = num2;
                            if (lane2.m_laneType == laneType && lane2.m_vehicleType == vehicleType)
                            {
                                int firstTarget = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_firstTarget;
                                int lastTarget = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_lastTarget;
                                if (currentTargetIndex < firstTarget || currentTargetIndex >= lastTarget)
                                {
                                    item2.ComparisonValue += Mathf.Max(1f, num13 * 3f - 3f) / ((num5 + lane2.m_speedLimit) * 0.5f * _maxLength);
                                }
                            }

                            AddBufferItem(item2, item.Position);
                        }
                    }
                }
                else if (lane2.m_laneType == laneType && lane2.m_vehicleType == vehicleType)
                {
                    num11++;
                }
                num2 = instance.m_lanes.m_buffer[(int)((UIntPtr)num2)].m_nextLane;
                num12++;
            }
            currentTargetIndex = num11;
            return result;
        }

        private void ProcessItem5(BufferItem item, ushort targetNode, ushort segmentId, ref NetSegment segment, byte connectOffset, int laneIndex, uint lane)
        {
            if ((segment.m_flags & (NetSegment.Flags.PathFailed | NetSegment.Flags.Flooded)) != NetSegment.Flags.None)
            {
                return;
            }
            var instance = Singleton<NetManager>.instance;
            var info = segment.Info;
            var info2 = instance.m_segments.m_buffer[item.Position.m_segment].Info;
            var num = info.m_lanes.Length;
            float num2;
            byte offset;
            if (segmentId == item.Position.m_segment)
            {
                Vector3 b = instance.m_lanes.m_buffer[(int)((UIntPtr)item.LaneId)].CalculatePosition(connectOffset * 0.003921569f);
                Vector3 a = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].CalculatePosition(connectOffset * 0.003921569f);
                num2 = Vector3.Distance(a, b);
                offset = connectOffset;
            }
            else
            {
                NetInfo.Direction direction = (targetNode != segment.m_startNode) ? NetInfo.Direction.Forward : NetInfo.Direction.Backward;
                Vector3 b2 = instance.m_lanes.m_buffer[(int)((UIntPtr)item.LaneId)].CalculatePosition(connectOffset * 0.003921569f);
                Vector3 a2;
                if ((byte)(direction & NetInfo.Direction.Forward) != 0)
                {
                    a2 = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].m_bezier.d;
                }
                else
                {
                    a2 = instance.m_lanes.m_buffer[(int)((UIntPtr)lane)].m_bezier.a;
                }
                num2 = Vector3.Distance(a2, b2);
                offset = (byte)(((byte)(direction & NetInfo.Direction.Forward) == 0) ? 0 : 255);
            }
            float num3 = 1f;
            float num4 = 1f;
            NetInfo.LaneType laneType = NetInfo.LaneType.None;
            if (item.Position.m_lane < info2.m_lanes.Length)
            {
                NetInfo.Lane lane2 = info2.m_lanes[item.Position.m_lane];
                num3 = lane2.m_speedLimit;
                laneType = lane2.m_laneType;
                num4 = CalculateLaneSpeed(connectOffset, item.Position.m_offset, ref instance.m_segments.m_buffer[item.Position.m_segment], lane2);
            }
            float averageLength = instance.m_segments.m_buffer[item.Position.m_segment].m_averageLength;
            float num5 = Mathf.Abs(connectOffset - item.Position.m_offset) * 0.003921569f * averageLength;
            float num6 = item.MethodDistance + num5;
            float num7 = item.ComparisonValue + num5 / (num4 * _maxLength);
            if (laneIndex < num)
            {
                NetInfo.Lane lane3 = info.m_lanes[laneIndex];
                BufferItem item2;
                item2.Position.m_segment = segmentId;
                item2.Position.m_lane = (byte)laneIndex;
                item2.Position.m_offset = offset;
                if (laneType != lane3.m_laneType)
                {
                    item2.MethodDistance = 0f;
                }
                else
                {
                    item2.MethodDistance = num6 + num2;
                }
                if (lane3.m_laneType != NetInfo.LaneType.Pedestrian || item2.MethodDistance < 1000f)
                {
                    item2.ComparisonValue = num7 + num2 / ((num3 + lane3.m_speedLimit) * 0.25f * _maxLength);
                    if (lane == _startLaneA)
                    {
                        float num8 = CalculateLaneSpeed(_startOffsetA, item2.Position.m_offset, ref segment, lane3);
                        float num9 = Mathf.Abs(item2.Position.m_offset - _startOffsetA) * 0.003921569f;
                        item2.ComparisonValue += num9 * segment.m_averageLength / (num8 * _maxLength);
                    }
                    if (lane == _startLaneB)
                    {
                        float num10 = CalculateLaneSpeed(_startOffsetB, item2.Position.m_offset, ref segment, lane3);
                        float num11 = Mathf.Abs(item2.Position.m_offset - _startOffsetB) * 0.003921569f;
                        item2.ComparisonValue += num11 * segment.m_averageLength / (num10 * _maxLength);
                    }
                    if ((segment.m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                    {
                        item2.Direction = NetInfo.InvertDirection(lane3.m_finalDirection);
                    }
                    else
                    {
                        item2.Direction = lane3.m_finalDirection;
                    }
                    item2.LaneId = lane;
                    item2.LanesUsed = (item.LanesUsed | lane3.m_laneType);
                    AddBufferItem(item2, item.Position);
                }
            }
        }

        private void AddBufferItem(BufferItem item, PathUnit.Position target)
        {
            uint num = _laneLocation[(int)((UIntPtr)item.LaneId)];
            uint num2 = num >> 16;
            int num3 = (int)(num & 65535u);
            int num6;
            if (num2 == _pathFindIndex)
            {
                if (item.ComparisonValue >= _buffer[num3].ComparisonValue)
                {
                    return;
                }
                int num4 = num3 >> 6;
                int num5 = num3 & -64;
                if (num4 < _bufferMinPos || (num4 == _bufferMinPos && num5 < _bufferMin[num4]))
                {
                    return;
                }
                num6 = Mathf.Max(Mathf.RoundToInt(item.ComparisonValue * 1024f), _bufferMinPos);
                if (num6 == num4)
                {
                    _buffer[num3] = item;
                    _laneTarget[(int)((UIntPtr)item.LaneId)] = target;
                    return;
                }
                int num7 = num4 << 6 | _bufferMax[num4]--;
                BufferItem bufferItem = _buffer[num7];
                _laneLocation[(int)((UIntPtr)bufferItem.LaneId)] = num;
                _buffer[num3] = bufferItem;
            }
            else
            {
                num6 = Mathf.Max(Mathf.RoundToInt(item.ComparisonValue * 1024f), _bufferMinPos);
            }
            if (num6 >= 1024)
            {
                return;
            }
            while (_bufferMax[num6] == 63)
            {
                num6++;
                if (num6 == 1024)
                {
                    return;
                }
            }
            if (num6 > _bufferMaxPos)
            {
                _bufferMaxPos = num6;
            }
            num3 = (num6 << 6 | ++_bufferMax[num6]);
            _buffer[num3] = item;
            _laneLocation[(int)((UIntPtr)item.LaneId)] = (_pathFindIndex << 16 | (uint)num3);
            _laneTarget[(int)((UIntPtr)item.LaneId)] = target;
        }
        private void GetLaneDirection(PathUnit.Position pathPos, out NetInfo.Direction direction, out NetInfo.LaneType type)
        {
            NetManager instance = Singleton<NetManager>.instance;
            NetInfo info = instance.m_segments.m_buffer[pathPos.m_segment].Info;
            if (info.m_lanes.Length > pathPos.m_lane)
            {
                direction = info.m_lanes[pathPos.m_lane].m_finalDirection;
                type = info.m_lanes[pathPos.m_lane].m_laneType;
                if ((instance.m_segments.m_buffer[pathPos.m_segment].m_flags & NetSegment.Flags.Invert) != NetSegment.Flags.None)
                {
                    direction = NetInfo.InvertDirection(direction);
                }
            }
            else
            {
                direction = NetInfo.Direction.None;
                type = NetInfo.LaneType.None;
            }
        }

        private void PathFindThread()
        {
            while (true)
            {
                while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    while (QueueFirst == 0u && !Terminated)
                    {
                        Monitor.Wait(QueueLock);
                    }
                    if (Terminated)
                    {
                        break;
                    }
                    Calculating = QueueFirst;
                    QueueFirst = PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_nextPathUnit;
                    if (QueueFirst == 0u)
                    {
                        QueueLast = 0u;
                        m_queuedPathFindCount = 0;
                    }
                    else
                    {
                        m_queuedPathFindCount--;
                    }
                    PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_nextPathUnit = 0u;
                    PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags = (byte)((PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags & -2) | 2);
                }
                finally
                {
                    Monitor.Exit(QueueLock);
                }
                try
                {
                    m_pathfindProfiler.BeginStep();
                    try
                    {
                        PathFindImplementation(Calculating, ref PathUnits.m_buffer[(int)((UIntPtr)Calculating)]);
                    }
                    finally
                    {
                        m_pathfindProfiler.EndStep();
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("path thread error: " + ex.Message);
                    UIView.ForwardException(ex);
                    Debug.LogError("Path find error: " + ex.Message + "\n" + ex.StackTrace, this);
                    var expr_1A0Cp0 = PathUnits.m_buffer;
                    var expr_1A0Cp1 = (UIntPtr)Calculating;
                    expr_1A0Cp0[(int)expr_1A0Cp1].m_pathFindFlags = (byte)(expr_1A0Cp0[(int)expr_1A0Cp1].m_pathFindFlags | 8);
                }
                while (!Monitor.TryEnter(QueueLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
                {
                }
                try
                {
                    PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags = (byte)(PathUnits.m_buffer[(int)((UIntPtr)Calculating)].m_pathFindFlags & -3);
                    Singleton<PathManager>.instance.ReleasePath(Calculating);
                    Calculating = 0u;
                    Monitor.Pulse(QueueLock);
                }
                finally
                {
                    Monitor.Exit(QueueLock);
                }
            }
        }
    }
}
