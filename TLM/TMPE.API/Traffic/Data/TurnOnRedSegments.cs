using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Traffic.Data {
	/**
	 * Holds left/right turn-on-red candidate segments
	 */
	public struct TurnOnRedSegments {
		/**
		 * Left segment id (or 0 if no left turn-on-red candidate segment)
		 */
		public ushort leftSegmentId;

		/**
		 * Right segment id (or 0 if no right turn-on-red candidate segment)
		 */
		public ushort rightSegmentId;

		public void Reset() {
			this.leftSegmentId = 0;
			this.rightSegmentId = 0;
		}

		public override string ToString() {
			return $"[TurnOnRedSegments {base.ToString()}\n" +
				"\t" + $"leftSegmentId = {leftSegmentId}\n" +
				"\t" + $"rightSegmentId = {rightSegmentId}\n" +
				"SegmentEnd]";
		}
	}
}
