# TM:PE -- /Custom/AI
Detoured *AI classes.
## Classes
- **CustomAmbulanceAI**: Path-finding detours for ambulances.
- **CustomBusAI**: Path-finding detours for busses. 
- **CustomCarAI**: Path-finding detours for cars. Updates current vehicle positions and prevents despawning (CustomSimulationStep), implements reckless driving, checks for custom speed limits (CalcMaxSpeed) and makes cars obey both priority rules and custom traffic lights (call to MayChangeSegment in CustomCalculateSegmentPosition).
- **CustomCargoTruckAI**: Path-finding detours for all kinds of cargo trucks. 
- **CustomCitizenAI**: Path-finding detours for citizens (= both residents and tourists). Manages usage of public transport and spawning of pocket cars.  
- **CustomFireTruckAI**: Path-finding detours for fire trucks. 
- **CustomHumanAI**: Implements checks for custom traffic lights for humans. 
- **CustomPassengerCarAI**: Path-finding detours for passenger cars. 
- **CustomPoliceCarAI**: Path-finding detours for police cars. 
- **CustomRoadAI**: Manages traffic density and current speed measurement values for every segment (CustomSegmentSimulationStep). Initiates the timed traffic light simulation (CustomNodeSimulationStep). Detours traffic light getters/setters in order to allow for custom traffic lights.
- **CustomShipAI**: Path-finding detours for ships.
- **CustomTaxiAI**: Path-finding detours for taxis. 
- **CustomTrainAI**: Path-finding detours for trains. Updates current vehicle positions and prevents despawning (CustomSimulationStep), checks for custom speed limits (TmCalculateSegmentPositionPathFinder) and makes trains obey both priority rules and custom traffic lights (call to MayChangeSegment in CustomCheckNextLane).
- **CustomTramBaseAI**: Path-finding detours for trams. Updates current vehicle positions and prevents despawning (CustomSimulationStep), checks for custom speed limits (CustomCalculateSegmentPosition and CustomCalculateSegmentPositionPathFinder) and makes trams obey both priority rules and custom traffic lights (call to MayChangeSegment in CustomCalculateSegmentPosition).
- **CustomTransportLineAI**: Path-finding detours for public transport lines.  
- **CustomVehicleAI**: Implements checks whether a vehicle may move to the next segment (MayChangeSegment). This includes checking priority rules and custom traffic lights. Checks for custom speed limits (CalculateSegPos).