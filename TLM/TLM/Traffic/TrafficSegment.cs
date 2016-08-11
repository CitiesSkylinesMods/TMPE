namespace TrafficManager.Traffic {
	/// <summary>
	/// A traffic segment (essentially a road) connects two nodes (Node1, Node2). One traffic segment
	/// can act as two different priority segments, one for each node.
	/// </summary>
	/// 
	public class TrafficSegment { // TODO remove this class
		public ushort Node1 = 0;
		public ushort Node2 = 0;

		public int Segment = 0;

		public SegmentEnd Instance1;
		public SegmentEnd Instance2;
	}
}
