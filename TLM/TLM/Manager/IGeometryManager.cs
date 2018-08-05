using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Geometry.Impl;
using TrafficManager.Util;
using static TrafficManager.Geometry.Impl.NodeGeometry;

namespace TrafficManager.Manager {
	public struct GeometryUpdate {
		public SegmentGeometry segmentGeometry { get; private set; }
		public NodeGeometry nodeGeometry { get; private set; }
		public SegmentEndReplacement replacement { get; private set; }

		public GeometryUpdate(SegmentGeometry segmentGeometry) {
			this.segmentGeometry = segmentGeometry;
			nodeGeometry = null;
			replacement = default(SegmentEndReplacement);
		}

		public GeometryUpdate(NodeGeometry nodeGeometry) {
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
		void OnUpdateSegment(SegmentGeometry geo);
		void OnSegmentEndReplacement(SegmentEndReplacement replacement);
		IDisposable Subscribe(IObserver<GeometryUpdate> observer);
		void MarkAsUpdated(SegmentGeometry geometry);
		void MarkAsUpdated(NodeGeometry geometry);
	}
}
