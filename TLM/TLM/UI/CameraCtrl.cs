using ColossalFramework;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TrafficManager.UI {
	class CameraCtrl {
		public static void GoToPos(Vector3 pos) {
			ToolsModifierControl.cameraController.SetOverrideModeOn(pos, Vector2.zero, 10.0f);
		}

		public static void ClearPos() {
			ToolsModifierControl.cameraController.SetOverrideModeOff();
		}

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

		internal static void GoToCitizenInstance(ushort citizenInstanceId, Vector3 pos) {
			InstanceID id = default(InstanceID);
			id.CitizenInstance = citizenInstanceId;

			ToolsModifierControl.cameraController.SetTarget(id, pos, true);
		}
	}
}
