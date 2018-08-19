using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry;
using TrafficManager.Util;

namespace TrafficManager.Manager {
	public struct GeometryUpdate {
		public ISegmentGeometry segmentGeometry { get; private set; }
		public INodeGeometry nodeGeometry { get; private set; }
		public SegmentEndReplacement replacement { get; private set; }

		public GeometryUpdate(ISegmentGeometry segmentGeometry) {
			this.segmentGeometry = segmentGeometry;
			nodeGeometry = null;
			replacement = default(SegmentEndReplacement);
		}

		public GeometryUpdate(INodeGeometry nodeGeometry) {
			this.nodeGeometry = nodeGeometry;
			segmentGeometry = null;
			replacement = default(SegmentEndReplacement);
		}

		public GeometryUpdate(SegmentEndReplacement replacement) {
			this.replacement = replacement;
			segmentGeometry = null;
			nodeGeometry = null;
		}
	}

	public interface IGeometryManager {
		// TODO define me!
		void SimulationStep(bool onylFirstPass=false);
		void OnUpdateSegment(ISegmentGeometry geo);
		void OnSegmentEndReplacement(SegmentEndReplacement replacement);
		IDisposable Subscribe(IObserver<GeometryUpdate> observer);
		void MarkAsUpdated(ISegmentGeometry geometry);
		void MarkAsUpdated(INodeGeometry geometry);
	}
}
