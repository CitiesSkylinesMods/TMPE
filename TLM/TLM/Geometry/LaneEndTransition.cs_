using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Geometry {
	public class LaneEndTransition {
		public enum LaneEndTransitionType {
			Invalid,
			LaneArrow,
			LaneConnection,
			Relaxed
		}

		/// <summary>
		/// Lane end where vehicles originate from
		/// </summary>
		public LaneEndGeometry SourceLaneEnd { get; private set; }

		/// <summary>
		/// Lane end where vehicle move to
		/// </summary>
		public LaneEndGeometry TargetLaneEnd { get; private set; }

		/// <summary>
		/// Lane distance metric
		/// </summary>
		public byte LaneDistance { get; set; }

		/// <summary>
		/// Lane connection type
		/// </summary>
		public LaneEndTransitionType TransitionType { get; set; }

		public override string ToString() {
			return $"[LaneEndTransition\n" +
				"\t" + $"SourceLaneEnd.LaneId = {SourceLaneEnd?.LaneId}\n" +
				"\t" + $"TargetLaneEnd.LaneId = {TargetLaneEnd?.LaneId}\n" +
				"\t" + $"LaneDistance = {LaneDistance}\n" +
				"\t" + $"TransitionType = {TransitionType}\n" +
				"LaneEndTransition]";
		}

		public LaneEndTransition(LaneEndGeometry sourceLaneEnd, LaneEndGeometry targetLaneEnd, LaneEndTransitionType transitionType) {
			SourceLaneEnd = sourceLaneEnd;
			TargetLaneEnd = targetLaneEnd;
			LaneDistance = 0;
			TransitionType = transitionType;
		}

		public LaneEndTransition(LaneEndGeometry sourceLaneEnd, LaneEndGeometry targetLaneEnd, LaneEndTransitionType transitionType, byte laneDistance) : this(sourceLaneEnd, targetLaneEnd, transitionType) {
			LaneDistance = laneDistance;
		}
	}
}
