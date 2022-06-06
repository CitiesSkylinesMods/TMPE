using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Manager.Model {
    internal struct SegmentEndPair<T> {

        public ushort segmentId;

        public T start;

        public T end;
    }
}
