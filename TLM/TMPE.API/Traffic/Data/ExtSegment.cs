using System;

namespace TrafficManager.Traffic.Data {
	public struct ExtSegment : IEquatable<ExtSegment> {
		/// <summary>
		/// Segment id
		/// </summary>
		public ushort segmentId;

		/// <summary>
		/// Segment valid?
		/// </summary>
		public bool valid;

		/// <summary>
		/// Is one-way?
		/// </summary>
		public bool oneWay;

		/// <summary>
		/// Is highway?
		/// </summary>
		public bool highway;

		/// <summary>
		/// Has bus lane?
		/// </summary>
		public bool buslane;

		public override string ToString() {
			return $"[ExtSegment {base.ToString()}\n" +
				"\t" + $"segmentId={segmentId}\n" +
				"\t" + $"valid={valid}\n" +
				"\t" + $"oneWay={oneWay}\n" +
				"\t" + $"highway={highway}\n" +
				"\t" + $"buslane={buslane}\n" +
				"ExtSegment]";
		}

		public ExtSegment(ushort segmentId) {
			this.segmentId = segmentId;
			valid = false;
			oneWay = false;
			highway = false;
			buslane = false;
		}

		public void Reset() {
			oneWay = false;
			highway = false;
			buslane = false;
		}

		public bool Equals(ExtSegment otherSeg) {
			return segmentId == otherSeg.segmentId;
		}

		public override bool Equals(object other) {
			if (other == null) {
				return false;
			}
			if (!(other is ExtSegment)) {
				return false;
			}
			return Equals((ExtSegment)other);
		}

		public override int GetHashCode() {
			int prime = 31;
			int result = 1;
			result = prime * result + segmentId.GetHashCode();
			return result;
		}
	}
}
