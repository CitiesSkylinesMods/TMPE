namespace TrafficManager.TrafficLight.Impl {

    internal class CustomSegment {
        public CustomSegmentLights StartNodeLights;
        public CustomSegmentLights EndNodeLights;

        public override string ToString() {
            return string.Format(
                "[CustomSegment \n\tStartNodeLights: {0}\n\tEndNodeLights: {1}\nCustomSegment]",
                StartNodeLights,
                EndNodeLights);
        }
    }
}