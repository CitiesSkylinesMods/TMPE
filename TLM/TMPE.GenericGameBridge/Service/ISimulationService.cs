using UnityEngine;

namespace GenericGameBridge.Service {
	public interface ISimulationService {
		bool LeftHandDrive { get; }
		uint CurrentBuildIndex { get; set; }
		uint CurrentFrameIndex { get; }
		Vector3 CameraPosition { get; }
	}
}
