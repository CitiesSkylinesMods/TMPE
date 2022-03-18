namespace TrafficManager.Util.Extensions {

    public static class PathUnitPositionExtensions {
        public static string ValuesToString(this PathUnit.Position position) {
            return $"[PathUnit] Segment: {position.m_segment} Lane: {position.m_lane} Offset: {position.m_offset}";
        }
    }
}