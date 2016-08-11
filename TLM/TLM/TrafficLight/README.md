# TM:PE -- /TrafficLight
Custom traffic light data structures.
## Classes
- **CustomSegment**: Holds references to both possible custom traffic lights at a segment (deprecated: can be merged with **TrafficSegment**)
- **CustomSegmentLights**: Holds all custom traffic lights for each vehicle type at a segment end.
- **CustomSegmentLight**: Stores a fully-directional traffic light state (red, yellow, green for left, forward, right) for one vehicle type at a segment end.
- **TimedTrafficLights**: Holds the traffic light program for every custom traffic light that is located at one junction. Aggregates all timed steps that are defined by the player (Steps). Implements the timed traffic light simulation (SimulationStep).
- **TimedTrafficLightsStep**: Represents one player-defined timed step for one junction, including minimum/maximum time (minTime, maxTime) and cached number of flowing/waiting vehicles (minFlow, maxWait).
- **TrafficLightSimulation**: Main class representing either a manual or timed traffic light at a junction.