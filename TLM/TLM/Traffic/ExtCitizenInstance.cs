using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.Traffic {
	public class ExtCitizenInstance {
		private ushort InstanceId;

		[Flags]
		public enum ExtFlags {
			None,
			CannotUsePassengerCar
		}

		public enum DepartureMode {
			None,
			/// <summary>
			/// 
			/// </summary>
			CalculatingPathToParkPos,
			/// <summary>
			/// 
			/// </summary>
			OnPathToParkPos,
			/// <summary>
			/// 
			/// </summary>
			ParkPosReached,
			/// <summary>
			/// 
			/// </summary>
			CalculatingCarPath,
			/// <summary>
			/// 
			/// </summary>
			OnCarPath
		}

		public ExtFlags Flags { get; internal set; }

		public DepartureMode CurrentDepartureMode { get; internal set; }

		public Vector3 ParkedVehicleTargetPosition { get; internal set; }

		public ExtCitizenInstance() {
			Reset();
		}

		public ExtCitizenInstance(ushort instanceId) {
			this.InstanceId = instanceId;
		}

		internal void Reset() {
			Flags = ExtFlags.None;
			CurrentDepartureMode = DepartureMode.None;
			ParkedVehicleTargetPosition = default(Vector3);
		}
	}
}
