namespace TrafficManager.TrafficLight.Impl {
    using TrafficManager.API.TrafficLight;

    internal class CustomSegment {
        public ICustomSegmentLights StartNodeLights;
        public ICustomSegmentLights EndNodeLights;

        public override string ToString() {
            return string.Format(
                "[CustomSegment \n\tStartNodeLights: {0}\n\tEndNodeLights: {1}\nCustomSegment]",
                StartNodeLights,
                EndNodeLights);
        }
    }
}