# TM:PE -- /Manager
Manager classes that allow for creating and controlling custom behavior.
## Classes
- **CustomTrafficLightsManager**: Manages all custom traffic lights and their states (both timed and manual) that are located at segment ends.
- **JunctionRestrictionManager**: Manages all junction restrictions.
- **LaneConnectionManager**: Manages custom lane connections made with the lane connector tool.
- **SpeedLimitManager**: Manages custom speed limits.
- **TrafficLightSimulationManager**: Manages the creation/deletion of manual and timed lights. Initiates the simulation of timed traffic lights (SimulationStep).
- **TrafficPriorityManager**: Manages priority signs. Implements calculation of priority rules at junctions with priority signs (HasIncomingVehiclesWithHigherPriority, HasVehiclePriority).
- **VehicleRestrictionsManager**: Manages custom vehicle restrictions.
- **VehicleStateManager**: Manages vehicle states (both positional and general data about vehicles that is being stored).