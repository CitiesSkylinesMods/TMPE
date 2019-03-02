using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager {
	public interface IJunctionRestrictionsManager {
		/// <summary>
		/// Determines if u-turn behavior may be controlled at the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <param name="node">node data</param>
		/// <returns><code>true</code> if u-turns may be customized, <code>false</code> otherwise</returns>
		bool IsUturnAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node);

#if TURNONRED
		/// <summary>
		/// Determines if turn-on-red behavior is enabled and may be controlled at the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <param name="node">node data</param>
		/// <returns><code>true</code> if turn-on-red may be customized, <code>false</code> otherwise</returns>
		bool IsTurnOnRedAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node);
#endif

		/// <summary>
		/// Determines if lane changing behavior may be controlled at the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <param name="node">node data</param>
		/// <returns><code>true</code> if lane changing may be customized, <code>false</code> otherwise</returns>
		bool IsLaneChangingAllowedWhenGoingStraightConfigurable(ushort segmentId, bool startNode, ref NetNode node);

		/// <summary>
		/// Determines if entering blocked junctions may be controlled at the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <param name="node">node data</param>
		/// <returns><code>true</code> if entering blocked junctions may be customized, <code>false</code> otherwise</returns>
		bool IsEnteringBlockedJunctionAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node);

		/// <summary>
		/// Determines if pedestrian crossings may be controlled at the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <param name="node">node data</param>
		/// <returns><code>true</code> if pedestrian crossings may be customized, <code>false</code> otherwise</returns>
		bool IsPedestrianCrossingAllowedConfigurable(ushort segmentId, bool startNode, ref NetNode node);

		/// <summary>
		/// Determines the default setting for u-turns at the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <param name="node">node data</param>
		/// <returns><code>true</code> if u-turns are allowed by default, <code>false</code> otherwise</returns>
		bool GetDefaultUturnAllowed(ushort segmentId, bool startNode, ref NetNode node);

#if TURNONRED
		/// <summary>
		/// Determines the default setting for turn-on-red at the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <param name="node">node data</param>
		/// <returns><code>true</code> if turn-on-red is allowed by default, <code>false</code> otherwise</returns>
		bool GetDefaultTurnOnRedAllowed(ushort segmentId, bool startNode, ref NetNode node);
#endif

		/// <summary>
		/// Determines the default setting for straight lane changes at the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <param name="node">node data</param>
		/// <returns><code>true</code> if straight lane changes are allowed by default, <code>false</code> otherwise</returns>
		bool GetDefaultLaneChangingAllowedWhenGoingStraight(ushort segmentId, bool startNode, ref NetNode node);

		/// <summary>
		/// Determines the default setting for entering a blocked junction at the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <param name="node">node data</param>
		/// <returns><code>true</code> if entering a blocked junction is allowed by default, <code>false</code> otherwise</returns>
		bool GetDefaultEnteringBlockedJunctionAllowed(ushort segmentId, bool startNode, ref NetNode node);

		/// <summary>
		/// Determines the default setting for pedestrian crossings at the given segment end.
		/// </summary>
		/// <param name="segmentId">segment id</param>
		/// <param name="startNode">at start node?</param>
		/// <param name="node">node data</param>
		/// <returns><code>true</code> if crossing the road is allowed by default, <code>false</code> otherwise</returns>
		bool GetDefaultPedestrianCrossingAllowed(ushort segmentId, bool startNode, ref NetNode node);
	}
}
