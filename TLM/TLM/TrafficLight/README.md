# TM:PE -- /TrafficLight
Custom traffic light data structures.
## Classes
- **CustomSegment**: Holds references to both possible custom traffic lights at a segment
- **CustomSegmentLight**: Stores a fully-directional traffic light state (red, yellow, green for left, forward, right) for one lane at a specific segment end.
- **CustomSegmentLights**: Holds a set of fully-directional traffic light states for all incoming lanes at a segment end.
- **TimedTrafficLights**: Holds the timed traffic light program for one specific junction or a set of junctions. Holds all timed steps that are defined by the player (Steps). Implements the timed traffic light program simulation (SimulationStep). (TODO: rework needed)
- **TimedTrafficLightsStep**: Represents one player-defined timed step for one junction, including minimum/maximum time (minTime, maxTime) and cached number of flowing/waiting vehicles (minFlow, maxWait).
- **TrafficLightSimulation**: Main class representing either a manual or timed traffic light at a junction.