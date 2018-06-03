using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Traffic;
using TrafficManager.Traffic.Enums;

namespace TrafficManager.Manager {
	public interface IVehicleStateManager {
		// TODO define me!
		// TODO documentation
		void SetNextVehicleIdOnSegment(ushort vehicleId, ushort nextVehicleId);
		void SetPreviousVehicleIdOnSegment(ushort vehicleId, ushort previousVehicleId);
		
		/// <summary>
		/// Handles a released vehicle.
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		void OnReleaseVehicle(ushort vehicleId, ref Vehicle vehicleData);

		/// <summary>
		/// Handles a created vehicle.
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		void OnCreateVehicle(ushort vehicleId, ref Vehicle vehicleData);

		/// <summary>
		/// Handles a despawned vehicle.
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		void OnSpawnVehicle(ushort vehicleId, ref Vehicle vehicleData);

		/// <summary>
		/// Handles a spawned vehicle.
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		void OnDespawnVehicle(ushort vehicleId, ref Vehicle vehicleData);

		/// <summary>
		/// Handles a vehicle when path-finding starts.
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="vehicleData">vehicle data</param>
		/// <param name="vehicleType">vehicle type to set, optional</param>
		/// <returns>vehicle type</returns>
		ExtVehicleType OnStartPathFind(ushort vehicleId, ref Vehicle vehicleData, ExtVehicleType? vehicleType);

		/// <summary>
		/// Retrieves the driver citizen instance id for the given vehicle.
		/// </summary>
		/// <param name="vehicleId">vehicle id</param>
		/// <param name="data">vehicle data</param>
		/// <returns></returns>
		ushort GetDriverInstanceId(ushort vehicleId, ref Vehicle data);
	}
}
