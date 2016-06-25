using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TrafficManager.UI {
	class CameraCtrl {

		public static void GoToBuilding(ushort buildingId, Vector3 pos) {
			InstanceID id = default(InstanceID);
			id.Building = buildingId;

			ToolsModifierControl.cameraController.SetTarget(id, pos, true);
		}

		public static void GoToVehicle(ushort vehicleId, Vector3 pos) {
			InstanceID id = default(InstanceID);
			id.Vehicle = vehicleId;

			ToolsModifierControl.cameraController.SetTarget(id, pos, true);
		}

		public static void GoToSegment(ushort segmentId, Vector3 pos) {
			InstanceID id = default(InstanceID);
			id.NetSegment = segmentId;

			ToolsModifierControl.cameraController.SetTarget(id, pos, true);
		}

		public static void GoToNode(ushort nodeId, Vector3 pos) {
			InstanceID id = default(InstanceID);
			id.NetNode = nodeId;

			ToolsModifierControl.cameraController.SetTarget(id, pos, true);
		}
	}
}
