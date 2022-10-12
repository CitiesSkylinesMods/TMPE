# TM:PE -- /Manager
Manager classes that allow for creating and controlling custom behavior.
## Classes
- **AbstractCustomManager**: Abstract base class for all custom managers. Provides virtual callback methods for performing actions on loading/saving/unloading the game.
- **AbstractNodeGeometryObservingManager**: Abstract base class for all custom managers that need to react to changes in node geometry.
- **AbstractSegmentGeometryObservingManager**: Abstract base class for all custom managers that need to react to changes in segment geometry.
- **AdvancedParkingManager**: Implements the main Parking AI logic (TODO: currently not everything is in there, some logic is still in the **CustomPassengerCarAI**, **CustomResidentAI**, **CustomTouristAI** and **CustomHumanAI** classes).
- **CustomSegmentLightsManager**: Manages all custom traffic lights that control actual traffic at in-game junctions
- **ExtBuildingManager**: Manages extended building information that is used by the Parking AI 
- **ExtCitizenInstanceManager**: Manages extended citizen instance information that is used by the Parking AI
- **ICustomDataManager**: Interface that is implemented by all managers that need to load/save data together with the savegame (note that the class **SerializableDataExtension** needs to know these managers during load/save). 
- **ICustomManager**: Interface that is implemented by all managers.
- **JunctionRestrictionManager**: Manages all junction restrictions (zebra crossings, entering blocked junctions, lane changing when going straight and u-turns).
- **LaneArrowManager**: Manages all custom lane arrows.
- **LaneConnectionManager**: Manages all custom lane connections that are drawn by the player.
- **OptionsManager**: Manages loading/saving mod options
- **ParkingRestrictionsManager**: Manages all custom parking restrictions where cars may be prohibited to park on selected road segments.
- **RoutingManager**: Implements custom path-finding logic for lane arrows, lane connections and highway rules (but not the actual Advanced Vehicle AI cost calculation, this is being done in **CustomPathFind**)
- **SegmentEndManager**: Manages all segment ends that keep track of vehicles approaching at priority signs and timed traffic lights
- **SpeedLimitManager**: Manages custom speed limits.
- **TrafficLightManager**: Offers traffic light toggling functionality and controls which junctions may have traffic lights
- **TrafficLightSimulationManager**: Manages the creation/deletion of manual and timed lights. Delegates the simulation of timed traffic lights (entry point: **SimulationStep**).
- **TrafficMeasurementManager**: Manages traffic measurement data used by the Advanced Vehicle AI
- **TrafficPriorityManager**: Manages priority signs and implements priority rules at junctions with priority signs (entry point: **HasVehiclePriority**).   
- **UtilityManager**: Offers auxiliary functions that must be executed within the simulation thread
- **VehicleBehaviorManager**: Implements vehicle behavior (mainly at junctions). Traffic light states and priority rule checking is delegated here.
- **VehicleRestrictionsManager**: Manages custom vehicle restrictions (custom bans for cars, cargo trucks, buses, etc.).
- **VehicleStateManager**: Manages vehicle states (both positional and general vehicle states are stored).

