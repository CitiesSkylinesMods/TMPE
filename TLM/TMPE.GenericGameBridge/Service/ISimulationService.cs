using ColossalFramework.Math;
using UnityEngine;

namespace GenericGameBridge.Service {
	public interface ISimulationService {
		bool LeftHandDrive { get; }
		uint CurrentBuildIndex { get; set; }
		uint CurrentFrameIndex { get; }
		Vector3 CameraPosition { get; }
		Randomizer Randomizer { get; }
		void AddAction(System.Action action);
		void PauseSimulation(bool forced);
		void ResumeSimulation(bool forced);
	}
}
