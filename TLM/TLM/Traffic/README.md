# TM:PE -- /Traffic
Auxiliary and traffic related data structures.
## Classes
- **ExtBuilding**: Extended building data. Used by Parking AI.
- **ExtCitizenInstance**: Extended citizen instance data. Used by Parking AI. 
- **ExtVehicleType**: Flag enum. Allows for finer-grained vehicle types.
- **PrioritySegment**: Data structure that holds priority signs.
- **SegmentEnd**: Represents the traffic situation at a segment end (only instantiated if either a priority sign or a timed traffic light is present). Holds player-defined priority signs (Type), currently registered vehicles (FirstRegisteredVehicleId; linked list, see **VehicleState**) and allows to count waiting and flowing traffic at the segment end (GetVehicleMetricGoingToSegment)
- **VehicleJunctionTransitState**: Enum. Allows to describe wheter a vehicle is currently approaching a junction (Approach), stopping in front of it (Stop), leaving it (Leave) or cannot pass the junction due to a traffic jam ahead (Blocked)
- **VehicleState**: Stores custom information about a vehicle (e.g. its **ExtVehicleType**, its current junction transit state or total length).