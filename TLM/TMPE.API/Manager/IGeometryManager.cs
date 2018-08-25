using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Traffic.Data;
using TrafficManager.Util;

namespace TrafficManager.Manager {
	public struct GeometryUpdate {
		public ExtSegment? segment { get; private set; }
		public ushort? nodeId { get; private set; }
		public SegmentEndReplacement replacement { get; private set; }

		public GeometryUpdate(ref ExtSegment segment) {
			this.segment = segment;
			nodeId = null;
			replacement = default(SegmentEndReplacement);
		}

		public GeometryUpdate(ushort nodeId) {
			this.nodeId = nodeId;
			segment = null;
			replacement = default(SegmentEndReplacement);
		}

		public GeometryUpdate(SegmentEndReplacement replacement) {
			this.replacement = replacement;
			segment = null;
			nodeId = null;
		}
	}

	public interface IGeometryManager {
		// TODO define me!
		void SimulationStep(bool onylFirstPass=false);
		void OnUpdateSegment(ref ExtSegment segment);
		void OnSegmentEndReplacement(SegmentEndReplacement replacement);
		IDisposable Subscribe(IObserver<GeometryUpdate> observer);
		void MarkAsUpdated(ref ExtSegment segment);
		void MarkAsUpdated(ushort nodeId);
	}
}
