using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace TrafficManager
{
    public class CustomPathManager : PathManager
    {
        CustomPathFind[] replacementPathFinds;

        public static CustomPathFind PathFindInstance;

        //On waking up, replace the stock pathfinders with the custom one
        new void Awake()
        {
            PathFind[] stockPathFinds = GetComponents<PathFind>();
            int l = stockPathFinds.Length;
            replacementPathFinds = new CustomPathFind[l];
            for (int i = 0; i < l; i++)
            {
                replacementPathFinds[i] = gameObject.AddComponent<CustomPathFind>();
                Destroy(stockPathFinds[i]);
            }
            typeof(PathManager).GetField("m_pathfinds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(this, replacementPathFinds);
        }

        public void UpdateWithPathManagerValues(PathManager stockPathManager)
        {
            // Needed fields come from joaofarias' csl-traffic
            // https://github.com/joaofarias/csl-traffic

            this.m_simulationProfiler = stockPathManager.m_simulationProfiler;
            this.m_drawCallData = stockPathManager.m_drawCallData;
            this.m_properties = stockPathManager.m_properties;
            this.m_pathUnitCount = stockPathManager.m_pathUnitCount;
            this.m_renderPathGizmo = stockPathManager.m_renderPathGizmo;
            this.m_pathUnits = stockPathManager.m_pathUnits;
            this.m_bufferLock = stockPathManager.m_bufferLock;
        }

        //
        // BEGIN STOCK CODE
        //

        public new bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPos, PathUnit.Position endPos, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength)
        {
            PathUnit.Position position = default(PathUnit.Position);
            return this.CreatePath(out unit, ref randomizer, buildIndex, startPos, position, endPos, position, position, laneTypes, vehicleTypes, maxLength, false, false, false, false);
        }

        public new bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength)
        {
            return this.CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, false, false, false, false);
        }

        public new bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue)
        {
            return this.CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue);
        }

        public new bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue)
        {
			return this.CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue, ItemClass.Service.None);
        }
        public new bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, ItemClass.Service vehicleService)
        {
            return this.CreatePath(out unit, ref randomizer, buildIndex, startPosA, startPosB, endPosA, endPosB, default(PathUnit.Position), laneTypes, vehicleTypes, maxLength, isHeavyVehicle, ignoreBlocked, stablePath, skipQueue, vehicleService);
        }

        public new bool CreatePath(out uint unit, ref Randomizer randomizer, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, bool skipQueue, ItemClass.Service vehicleService)
        {
            while (!Monitor.TryEnter(this.m_bufferLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            uint num;
            try
            {
                if (!this.m_pathUnits.CreateItem(out num, ref randomizer))
                {
                    unit = 0u;
                    bool result = false;
                    return result;
                }
                this.m_pathUnitCount = (int)(this.m_pathUnits.ItemCount() - 1u);
            }
            finally
            {
                Monitor.Exit(this.m_bufferLock);
            }
            unit = num;

            byte simulationFlags = createSimulationFlag(isHeavyVehicle, ignoreBlocked, stablePath, vehicleService);
            assignPathProperties(unit, buildIndex, startPosA, startPosB, endPosA, endPosB, vehiclePosition, laneTypes, vehicleTypes, maxLength, simulationFlags);

            return findShortestPath(unit, skipQueue);
        }

        public static uint GetLaneID(PathUnit.Position pathPos)
        {
            NetManager instance = Singleton<NetManager>.instance;
            uint num = instance.m_segments.m_buffer[(int)pathPos.m_segment].m_lanes;
            int num2 = 0;
            while (num2 < (int)pathPos.m_lane && num != 0u)
            {
                num = instance.m_lanes.m_buffer[(int)((UIntPtr)num)].m_nextLane;
                num2++;
            }
            return num;
        }

        private void assignPathProperties(uint unit, uint buildIndex, PathUnit.Position startPosA, PathUnit.Position startPosB, PathUnit.Position endPosA, PathUnit.Position endPosB, PathUnit.Position vehiclePosition, NetInfo.LaneType laneTypes, VehicleInfo.VehicleType vehicleTypes, float maxLength, byte simulationFlags){
            
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_simulationFlags = simulationFlags;

            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_pathFindFlags = 0;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_buildIndex = buildIndex;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position00 = startPosA;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position01 = endPosA;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position02 = startPosB;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position03 = endPosB;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_position11 = vehiclePosition;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_nextPathUnit = 0u;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_laneTypes = (byte)laneTypes;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_vehicleTypes = (byte)vehicleTypes;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_length = maxLength;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_positionCount = 20;
            this.m_pathUnits.m_buffer[(int)((UIntPtr)unit)].m_referenceCount = 1;
        }

        private bool findShortestPath(uint unit, bool skipQueue){
                    
            int maxPathLength = 10000000;
            CustomPathFind pathFind = null;
            for (int i = 0; i < this.replacementPathFinds.Length; i++)
            {
                CustomPathFind pathFind2 = this.replacementPathFinds[i];
                if (pathFind2.IsAvailable && pathFind2.m_queuedPathFindCount < maxPathLength)
                {
                    maxPathLength = pathFind2.m_queuedPathFindCount;
                    pathFind = pathFind2;
                }
            }

            if (pathFind != null && pathFind.CalculatePath(unit, skipQueue))
            {
                return true;
            }
            this.ReleasePath(unit);
            return false;
        }

        private byte createSimulationFlag(bool isHeavyVehicle, bool ignoreBlocked, bool stablePath, ItemClass.Service vehicleService){
        	byte simulationFlags = 1;

        	if (isHeavyVehicle)
            {
               	simulationFlags |= 16;
            }
            if (ignoreBlocked)
            {
            	simulationFlags |= 32;
            }
            if (stablePath)
            {
             	simulationFlags |= 64;
            }

            byte vehicleFlag = 0;
            if (vehicleService == ItemClass.Service.None)
            	return simulationFlags; //No VehicleFlag needed.
            else if (vehicleService == ItemClass.Service.Commercial)
                vehicleFlag |= 128;
            else if (vehicleService == ItemClass.Service.FireDepartment)
                vehicleFlag |= 130;
            else if (vehicleService == ItemClass.Service.Garbage)
                vehicleFlag |= 132;
            else if (vehicleService == ItemClass.Service.HealthCare)
                vehicleFlag |= 134;
            else if (vehicleService == ItemClass.Service.Industrial)
                vehicleFlag |= 136;
            else if (vehicleService == ItemClass.Service.PoliceDepartment)
                vehicleFlag |= 138;
            else if (vehicleService == ItemClass.Service.PublicTransport)
                vehicleFlag |= 140;
            
			simulationFlags |= vehicleFlag;

			return simulationFlags;
        }
    }
}
