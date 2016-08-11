# TM:PE -- /Traffic
Auxiliary and traffic related data structures.
## Classes
- **ArrowDirection**: Enum. Represents compass-like (NOSW) directions.
- **ExtVehicleType**: Flag enum. Allows for finer-grained vehicle types.
- **SegmentEnd**: Represents the traffic situation at a segment end (only instantiated if either a priority sign or a timed traffic light is present). Holds player-defined priority signs (Type), currently registered vehicles (FirstRegisteredVehicleId; linked list, see **VehicleState**) and allows to count waiting and flowing traffic at the segment end (GetVehicleMetricGoingToSegment)
- **TrafficSegment**: Holds both possible segment ends of a segment.
- **VehicleJunctionTransitState**: Enum. Allows to describe wheter a vehicle is currently approaching a junction (Enter), stopping in front of it (Stop), leaving it (Leave) or cannot pass the junction due to a traffic jam ahead (Blocked)
- **VehicleState**: Stores custom information about a vehicle (e.g. its **ExtVehicleType**, its current junction transit state or total length). Holds a reference to the current segment end (CurrentSegmentEnd) and to both the previous and next vehicle that are currently located at the same segment end (PreviousVehicleIdOnSegment and NextVehicleIdOnSegment). Allows GC-friendly **PathUnit** calculations (ProcessPathUnit methods).