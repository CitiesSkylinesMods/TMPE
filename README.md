# Cities: Skylines - Traffic Manager: *President Edition* [![Discord](https://img.shields.io/discord/545065285862948894.svg)](https://discord.gg/faKUnST)
A modification for **Cities: Skylines** to add additional traffic control.

User manuals:

[Old documentation - from original version but still perfectly compatible](http://www.viathinksoft.de/tmpe/wiki)

[New documentation - work-in-progress (thanks to @aubergine10)](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki)


# Changelog
1.10.15, 10/02/2019
- Enhancement: Now you can use Escape key to close Traffic Manager without returning to Pause Menu (thanks to @aubergine10 for suggestion) (#16)
- Gameplay: Updated pathfinding with missing vanilla logic
- Gameplay: Tweaked values in CargoTruckAI path finding (thanks to @pcfantasy for improvement suggestion)
- Gameplay: Tweaked speed multiplier of reckless drivers to get more realistic speed range (thanks to @aubergine10 for suggestion) (#23)
- UI: New icons for cargo and passenger train restriction (thanks to @aubergine10) (#17)
- Translations: Simplified Chinese translation updated (thanks to @Emphasia for translating)
- Other: Added notification if user is still subscribed to old original TM:PE
- [Experimental feature] Turn on red (thanks to @FireController1847 for implementation and to @pcfantasy for source code base)

1.10.14, 27/01/2019
- Bugfix: Added missing Car AI type (postVanAI) - now post vans and post trucks are assigned to service vehicles group 
- Bugfix: Vehicles doesn't stop when driving through toll booth - fixes toll booth income too
- Bugfix: Cargo Airport doesn't work (Cargo planes not spawning and not arriving)
- Updated Polish translation
- Updated Korean translation (thanks to @Toothless FLY [ROK]LSh.st for translating)
- Fixed Mod Options layout (text label overlaps slider control if too wide)

1.10.13, 31/10/2018
- Bugfix: Tollbooth fix

1.10.12, 08/12/2018
- Added the option to allow/disallow vehicles to enter a blocked junction at transition and pedestrian crossing nodes (#195)
- Updated Russian translation (thanks to vitalii2011 for translating)
- Bent nodes do not allow for u-turns by default (#170)
- Bugfix: Emergency vehicles pass closed barriers at level crossings
- Bugfix: Bus lines render u-turn where they should not (#207)
- Bugfix: Parking AI: Cims leaving the city despawn their car at public transport stations (#214)
- Bugfix: Crossing restrictions do not work at intersection between road and highway (#212)

1.10.11, 07/21/2018
- U-turn lane connections are represented by appropriate lane arrow (#201)
- Bugfix: Heavy vehicles are unable to u-turn at dead ends (#194)
- Bugfix: Routing & Priority rules do not work properly for acute (< 30°)/obtuse(> 150°) segment angles (#199)
- Bugfix: Buses do not prefer lanes with correct lane arrow (#206)
- Bugfix: Race condition in path-finding might cause paths to be assigned to wrong vehicle/citizen (#205)
- Bugfix: Vehicles are unable to perform u-turns when setting off on multi-lane roads (#197)

1.10.10, 07/14/2018
- Parking AI: Improved park & ride behavior
- Parking AI: Walking paths from parking position to destination building take public transportation into account
- Bugfix: Parking AI causes unnecessary path-findings (#183, thanks to Sipke82 for reporting)
- Bugfix: Prohibiting cims from crossing the road also affect paths where crossing is unnecessary (#168, thanks to aubergine10 for reporting) 

1.10.9, 07/13/2018
- Updated for game version 1.10.1-f3
- Re-implemented path-finding algorithm
- Updated French translation (thanks to mjm92150 for translating!)

1.10.8, 07/01/2018
- Updated Korean translation (thanks to @Toothless FLY [ROK]LSh.st for translating)
- Updated Polish translation (thanks to @Krzychu1245 for translating)
- Added button to remove parked vehicles (in options dialog, see maintenance tab)
- Parking AI: Removed check for distance between parked vehicle and target building
- Bugfix: Parking AI: Cims spawn pocket cars when they originate from an outside connection
- Bugfix: Incorrect speed limits returned for pedestrian lanes
- Bugfix: Routing is not updated while the game is paused (thanks to @Oh My Lawwwd! for reporting)
- Bugfix: Vanilla traffic lights are ignored when either the priority signs or timed traffic light features are disabled (thanks to @aubergine10 for reporting)
- Bugfix: Park maintenance vehicles are not recognized as service vehicles
- Bugfix: Cars leaving city state "thinking of a good parking spot" (thanks to @aubergine10 for reporting)

1.10.7, 05/28/2018
- Bugfix: U-turn routing is inconsistent on transport lines vs. bus paths (#137, thanks to @Zorgoth for reporting this issue)
- Bugfix: Junction restrictions for pedestrian crossings are sometimes not preserved (#142, thanks to Anrew and @wizardrazer for reporting this issue)
- Fixed: Geometry subscription feature may cause performance issues (#145)
- Fixed: Parking AI: Transport mode storage causes performance issues during loading (#147, thanks to @hannebambel002 and @oneeyets for reporting and further for providing logs and savegames)

1.10.6, 05/24/2018
- Updated for game version 1.10.0-f3
- Accessibility: New option: Main menu size can be controlled
- Accessibility: New option: GUI and overlay transparency can be controlled
- New option: Penalties for switching between different public transport lines can be toggled
- Cims can now be removed from the game
- Improved window design
- Path-finding: Service vehicles are now allowed to ignore lane arrows right after leaving their source building, thus service buildings should now work properly at dead-end roads with median
- Lane connector can be used on monorail tracks
- Advanced Vehicle AI: Tuned parameters
- Dynamic Lane Selection: Absolute speed measurements are used instead of relative measurements
- Improved randomization for realistic speeds such that vehicles may change their target velocity over time
- Improved vehicle position tracking
- Improved mod compatibility checks
- Parking AI: Improved behavior in situations where vehicles are parked near public transport hubs and road connections are partially unavailable
- Bugfix: Parking AI: Not all possible paths are regarded during path-finding
- Bugfix: Parking AI: Cims become confused when trying to return their abandoned car back home (special thanks to Wildcard-25 for reporting and solving this issue)
- Bugfix: Parking AI: Cims do not search for parking building when road-side parking spaces are found
- Bugfix: Parking AI: Parked vehicles are spawned near the source building even when cims are already en route
- Bugfix: Parking AI: Cims sometimes get stuck in an infinite loop while trying to enter their parked car
- Bugfix: Lane connector does not work for roads with more than ten lanes
- Bugfix: Allowing/Disallowing vehicles to enter a blocked junction does not work for certain junctions
- Updated Korean translation (thanks to @Toothless FLY [ROK]LSh.st for translating)

1.10.5, 01/06/2018
- UI scaling removed
- Simplified Chinese translation updated (thanks to Emphasia for translating)
- Polish translation updated (thanks to @Krzychu1245 for translating)
- Introduced randomization for lane changing costs
- Introduced randomization for "trucks prefer innermost lanes on highways" costs
- Removed unnecessary calculations in path-finding
- Added path-finding costs for public transport transitions
- Pedestrian traffic lights do not show up if crossing the street is prohibited
- Busses are allowed to switch multiple lanes after leaving a bus stop
- Bugfix: Main menu button might be out of view
- Bugfix: Division by zero occurs for low speed roads
- Bugfix: Automatic pedestrian lights at railroad do not work as expected
- Bugfix: Timed traffic lights show up for bicycles (they should not)
- Bugfix: Due to a multi-threading issue junction restrictions may cause the game state to become inconsistent
- Bugfix: Routing rules prevents vehicles from spawning when starting building lies too close to an intersection/road end
- Bugfix: Disabling tutorial message has no effect
- Bugfix: "Stay on lane" feature does not work as intended for certain nodes

1.10.4, 10/19/2017
- Updated for game version 1.9.0-f5
- Added possibility to add priority signs at multiple junctions at once (press Shift)
- Added tutorials (can be disabled in the options window globally)

1.10.3, 08/18/2017
- Bugfix: Setting unlimited speed limit causes vehicles to crawl at low speed (thanks to @sethisuwan for reporting this issue)
- Bugfix: Vehicle-separated traffic lights do not show up for trams & monorails (thanks to @thecitiesdork for reporting this issue)

1.10.2, 08/17/2017
- Updated for game version 1.8.0-f3
- Improved performance
- Bugfix: Pedestrians sometimes ignore red traffic light signals (thanks to @(c)RIKUPI™ for reporting this issue)
- Bugfix: Timed traffic lights do not correctly recognize set vehicle restrictions (thanks to @alborzka for reporting this issue)
  
1.10.1, 08/05/2017
- Updated Polish, Korean, and Simplified Chinese translations
- Bugfix: Default routing is disabled if the lane connector is used on a subset of all available lanes only
- Bugfix: Parking AI cannot be enabled/disabled
- Bugfix: Lane connection points can connected to themselves

1.10.0, 07/30/2017
- New feature: Dynamic Lane Selection
- New feature: Adaptive step switching
- New feature: Individual vehicles may be removed from the game 
- New option: Vehicle restrictions aggression
- New option: Vehicles follow priority rules at junctions with timed traffic lights
- Improved path-finding performance
- Improved traffic measurement engine performance 
- Reorganized global configuration file (sorry, your main menu and main button positions are reset)
- The option "Road condition has a bigger impact on vehicle speed" is only shown if the Snowfall DLC is owned
- The flow/wait calculation mode to be used is now configurable via the global configuration file
- Added path-find statistics label
- Added confirmation dialog for "Clear Traffic" button
- Currently active timed traffic light step is remembered
- Trains do not wait for each other anymore near timed traffic lights
- It is now possible to connect train station tracks and outside connections with the lane connector
- Disabling the Parking AI triggers graceful clean up procedure
- Relocated some options
- Improved vehicle state tracking
- Workaround for a base game issue that causes trams to get stuck
- Trains do not longer stop in front of green timed traffic lights
- Vehicles use queue skipping to prioritize path-finding runs that are caused by road modifications
- Adding a vehicle separate light to a timed traffic lights applies the main light configuration
- Parking AI: Vehicles can now find parking spaces at the opposite road side
- Parking AI: Included an improved fallback logic for some edge cases
- Parking AI: Citizens should now be more successful in returning their cars back home  
- Parking AI: Tuned parking radius parameters 
- Parking AI: If the limit for parked vehicles is reached and parking fails due to it, no alternative parking space is queried
- Vehicle AI: Busses prefer lanes with correct lane arrow over incorrect ones
- Bugfix: Using the bulldozer tool might lead to inconsistent road geometry information
- Bugfix: Citizens that fail to approach their parked car fly towards their target building 
- Bugfix: Parking AI: Path-finding fails if cars are parked too far away from a road
- Bugfix: Parking AI: Citizens approaching a car start to float away
- Bugfix: "Heavy vehicles prefer outer lanes on highways" does not work
- Bugfix: The lane connector does not allow connecting all available lane end points at train stations and on bidirectional one-lane train tracks
- Bugfix: Vehicles may get stuck in several situations
- Upgrading to a road with bus lanes now copies an already existing traffic light state to the new traffic light 

1.9.6, 05/28/2017
- Updated Simplified Chinese translation
- Bugfix: Vehicles cannot perform u-turns at junctions with only one outgoing segment (thanks to @Sunbird for reporting this issue)
- Bugfix: Path-finding costs for large distances exceed the maximum allowed value (thanks to @Huitsi for reporting this issue)
- Bugfix: Under certain circumstances path-finding at railroad crossings allow switching from road to rail tracks. 

1.9.5, 05/24/2017
- Updated for game version 1.7.1-f1
- Updated Polish, Korean and Italian translation
- Language can now be switched without requiring a game restart
- Bugfix: Routing calculation does not work as expected for one-way roads with tram tracks (thanks to @bigblade66, @Battelman2 and @AS_ for reporting and providing extensive information)
- Bugfix: Copying timed traffic lights lead to inconsistent internal states which causes timed traffic lights to be omitted during the save process (thanks to @jakeroot and @t1a2l for reporting this issue)
- Bugfix: In certain situations unnecessary vehicle-seperate traffic lights are being created
- Bugfix: Upgrading a train track segment next to a timed traffic light causes trains to ignore the traffic light
- Hotfix: Cable cars despawn at end-of-line stations

1.9.4, 05/23/2017
- New option: Ban private cars and trucks on bus lanes
- Updated Spanish and French translation 
- Optimized path-finding
- Increased path-finding cost for private cars driving on bus lanes
- Increased path-finding cost for disregarding vehicle restrictions 
- Bugfix: Path-finding is unable to calculate certain paths after modifying the road network

1.9.3, 05/22/2017
- Disabled notification of route recalculating because some players report crashes
- Removed default vehicle restrictions from bus lanes
- Modified junction restrictions come into effect instantaneously
- UI: Saving a timed step does not reset the timed traffic light to the first state
- Bugfix: AI: Segment traffic data is not taken into account 
- Bugfix: Priority rules are not properly obeyed
- Bugfix: Under certain circumstances priority signs cannot be removed 
- Bugfix: Path-finding is unable to calculate certain paths

1.9.2, 05/20/2017
- UI: Main menu & UI tools performance improved
- Bugfix: Traffic lights can be removed from junctions that are controlled by a timed traffic light program

1.9.1, 05/19/2017
- Updated French, Dutch and Korean translation
- Bugfix: Using the vanilla traffic light toggling feature crashes the game if TMPE's main menu has not been opened at least once
- Bugfix: AI: More car traffic and less public transportation present than in vanilla

1.9.0, 05/18/2017
- Updated for game version 1.7.0-f5
- New feature: Parking restrictions
- New feature: Speed limits can be set up for individual lanes with the Control key
- New feature: Added timed traffic light and speed limit support for monorails
- New feature: Copy & paste for individual timed traffic lights
- New feature: Rotate individual timed traffic lights
- New feature: Lane customizations may come into effect instantaneously
- Unified traffic light toggling feature with game code
- Performance improvements
- Reworked the way that traffic measurements are performed
- Advanced Vehicle AI: Algorithm updated, performance improved - Possible routing decisions are now being precalculated
- Path-finding cost multiplicator for vehicle restrictions is now configurable in TMPE_GlobalConfig.xml
- UI: More compact, movable main menu UI
- Added support for custom languages
- Added Korean translation (thanks to @Toothless FLY [ROK]LSh.st for translating)
- Updated translations: German, Polish, Russian, Portuguese, Traditional Chinese 
- Major code refactorings
- AI: Tuned path-finding parameters
- New option: Main button position can be locked
- New option: Main menu position can be locked
- New option: Added language selection in options dialog
- New option: Customization of lane arrows, lane connections and vehicle restrictions can now come into effect instantaneously 
- Bugfix: Cars sometimes get stuck forever when the Advanced Parking AI is activated (thanks to @cmfcmf for reporting this issue)
- Bugfix: Busses do not perform u-turns even if the transport line show u-turns (thanks to @dymanoid for reporting this issue)
- Bugfix: Timed traffic lights do not work as expected on single-direction train tracks (thanks to @DaEgi01 for reporting this issue)
- Bugfix: Vehicle restriction and speed limit signs overlay is displayed on the wrong side of inverted road segments
- Bugfix: Influx statistics value is zero (thanks to @hjo for reporting this issue) 

1.8.16, 03/20/2017
- Lane connections can now also be removed by pressing the backspace key
- Improved lane selection for busses if the option "Busses may ignore lane arrows" is activated
- Bugfix: The game sometimes freezes when using the timed traffic light tool
- Bugfix: Lane connections are not correctly removed after modifying/removing a junction
- Bugfix: Selecting a junction for setting up junction restrictions toggles the currently hovered junction restriction icon

1.8.15, 01/27/2017
- Updated for game version 1.6.3-f1

1.8.14, 01/07/2017
- Bugfix: Wait/flow ratio at timed traffic lights is sometimes not correctly calculated
- Bugfix: A deadlock situation can arise at junctions with priority signs such that no vehicle enters the junction 
- Bugfix: When adding a junction to a timed traffic light, sometimes light states given by user input are not correctly stored
- Bugfix: Joining two timed traffic lights sets the minimum time to "1" for steps with zero minimum time assigned
- Bugfix: Modifications of timed traffic light states are sometimes not visible while editting the light (but they are applied nonetheless)
- Bugfix: Button background is not always correctly changed after clicking on a button within the main menu 
- Tram lanes can now be customized by using the lane connector tool
- Minor performance optimizations for priority sign simulation

1.8.13, 01/05/2017
- Bugfix: Timed traffic ligt data can become corrupt when upgrading a road segment next to a traffic light, leading to faulty UI behavior (thanks to @Brain for reporting this issue)
- Bugfix: The position of the main menu button resets after switching to the free camera mode (thanks to @Impact and @gravage for reporting this issue)
- Bugfix: A division by zero exception can occur when calculating the average number of waiting/floating vehicles
- Improved selection of overlay markers on underground roads (thanks to @Padi for reminding me of that issue)
- Minor performance improvements

1.8.12, 01/02/2017
- Updated for game version 1.6.2-f1
- Bugfix: After leaving the "Manual traffic lights" mode the traffic light simulation is not cleaned up correctly (thanks to @diezelunderwood for reporting this issue)
- Bugfix: Insufficient access rights to log file causes the mod to crash

1.8.11, 01/02/2017
- Bugfix: Speed limits for elevated/underground road segments are sometimes not correctly loaded (thanks to @Pirazel and @[P.A.N] Uf0 for reporting this issue)

1.8.10, 12/31/2016
- Improved path-finding performance (a bit)
- Added a check for invalid road thumbnails in the "custom default speed limits" dialog

1.8.9, 12/29/2016
- It is now possible to set speed limits for metro tracks
- Custom default speed limits may now be defined for train and metro tracks
- Junction restrictions may now be controlled at bend road segments
- Customizable junctions are now highlighted by the lane connector tool
- Improved UI behavior
- Performance improvements
- Bugfix: Selecting a junction to set up priority signs sometimes does not work (thanks to @Artemis *Seven* for reporting this issue)
- Bugfix: Automatic pedestrian lights do not work as expected at junctions with incoming one-ways and on left-hand traffic maps

1.8.8, 12/25/2016
- Bugfix: Taxis are not being used
- Bugfix: Prohibiting u-turns with the junction restriction tool does not work (thanks to @Kisoe for reporting this issue)
- Bugfix: Cars are sometimes floating across the map while trying to park (thanks to @[Delta ²k5] for reporting this issue)

1.8.7, 12/24/2016
- Bugfix: Parking AI: Cims that try to reach their parked car are sometimes teleported to another location where they start to fly through the map in order to reach their car
- Bugfix: Parking AI: Cims owning a parked car do not consider using other means of transportation
- Bugfix: Parking AI: Residents are unable to leave the city through a highway outside connection 
- Bugfix: Trains/Trams are sometimes not detected at timed traffic lights
- Advanced AI: Improved lane selection
- The position of the main menu button is now forced inside screen bounds on startup
- Improved overall user interface performance
- Improved overlay behavior
- Improved traffic measurement
- Auto pedestrian lights at timed traffic lights behave more intelligently now
- A timed traffic light step with zero minimum time assigned can now be skipped automatically
- Using the lane connector to create a u-turn now automatically enables the "u-turn allowed" junction restriction
- Updated French translation (thanks to @simon.royer007 for translating)
- Added Italian translation (thanks to @Admix for translating)  

1.8.6, 12/12/2016
- Added Korean language (thanks to @Toothless FLY [ROK]LSh.st for translating)
- Updated Chinese language code (zh-cn -> zh) in order to make it compatible with the game (thanks to @Lost丶青柠 for reporting this issue)

1.8.5, 12/11/2016
- Updated to game version 1.6.1-f2
- Removed option "Evacuation busses may only be used to reach a shelter" (CO fixed this issue)
- Bugfix: Average speed limits are not correctly calculated for road segments with bicycle lanes (thanks to @Toothless FLY [ROK]LSh.st for reporting this issue)

1.8.4, 12/11/2016
- New feature: "Stay on lane": By pressing Shift + S in the Lane Connector tool you can now link connected lanes such that vehicles are not allowed to change lanes at this point. Press Shift + S again to restrict "stay on lane" to either road direction.
- U-turns are now only allowed to be performed from the innermost lane     
- TMPE now detects if the number of spawned vehicles is reaching its limit (16384). If so, spawning of service/emergency vehicles is prioritized over spawning other vehicles. 
- Bugfix: Bicycles cannot change from bicycle lanes to pedestrian lanes
- Bugfix: Travel probabilities set in the "Citizen Lifecycle Rebalance v2.1" mod are not obeyed (thanks to @informmanuel, @shaundoddmusic for reporting this issue)
- Bugfix: Number of tourists seems to drop when activating the mod (statistics were not updated, thanks to @hpp7117, @wjrohn for reporting this issue)
- Bugfix: When loading a second savegame a second main menu button is displayed (thanks to @Cpt. Whitepaw for reporting this issue)
- Bugfix: While path-finding is in progress vehicles do "bungee-jumping" on the current segment (thanks to @mxolsenx, @Howzitworld for reporting this issue)
- Bugfix: Cims leaving the city search for parking spaces near the outside connection which is obviously not required   

1.8.3, 12/4/2016
- Bugfix: Despite having the Parking AI activated, cims sometimes still spawn pocket cars.
- Bugfix: When the Parking AI is active, bicycle lanes are not used (thanks to @informmanuel for reporting this issue)
- Tweaked u-turn behavior
- Improved info views 

1.8.2, 12/3/2016
- Bugfix: Taxis were not used (thanks to @[Delta ²k5] for reporting)
- Bugfix: Minor UI fix in Default speed limits dialog

1.8.1, 12/1/2016
- Updated translations: Polish, Chinese (simplified)
- Bugfix: Mod crashed when loading a second savegame 

1.8.0, 11/29/2016
- Updated to game version 1.6.0-f4
- New feature: Default speed limits
- New feature: Parking AI (replaces "Prohibit cims from spawning pocket cars")
- New option: Heavy vehicles prefer outer lanes on highways
- New option: Realistic speeds
- New option: Evacuation busses may ignore traffic rules (Natural Disasters DLC required)
- New option: Evacuation busses may only be used to reach a shelter (Natural Disasters DLC required)
- AI: Improved lane selection, especially on busy roads
- AI: Improved mean lane speed measurement
- Traffic info view shows parking space demand if Parking AI is activated
- Public transport info view shows transport demand if Parking AI is activated
- Added info texts for citizen and vehicle tool tips if Parking AI is activated
- Extracted internal configuration to XML configuration file
- Changed main menu button due to changes in the game's user interface
- Main menu button is now moveable
- Removed compatibility check for Traffic++ V2 (Traffic++ V2 is no longer compatible with TMPE because maintaining compatibility is no longer feasible due to the high effort)
- Updated translations: German, Portuguese, Russian, Dutch, Chinese (traditional)

1.7.15, 10/26/2016
- Bugfix: Timed traffic lights window disappears when clicking on it with the middle mouse button (thanks to @Nexus and @Mariobro14 for helping me identifying the cause of this bug)

1.7.14, 10/18/2016 
- Updated for game version 1.5.2-f3

1.7.13, 09/15/2016
- Implemented a permanent fix to solve problems with stuck vehicles/cims caused by third party mods
- Added a button to reset stuck vehicles/cims (see mod settings menu)
- AI: Improved lane selection algorithm
- Bugfix: AI: Lane merging was not working as expected
- Bugfix: Pedestrian light states were sometimes not being stored correctly (thanks to Filip for pointing out this problem)

1.7.12, 09/09/2016
- AI: Lane changes are reduced on congested road segments
- Timed traffic lights should now correctly detect trains and trams
- Bugfix: GUI: Junction restriction icons sometimes disappear
- Updated Chinese (simplified) translation

1.7.11, 09/01/2016
- Updated to game version 1.5.1-f3

1.7.10, 08/31/2016
- Players can now disable spawning of pocket cars
- Updated Chinese (simplified) translation
- Bugfix: Timed traffic lights were flickering
- Bugfix: Pedestrian traffic lights were not working as expected
- Bugfix: When upgrading/removing/adding a road segment, nearby junction restrictions were removed
- Bugfix: Setting up vehicle restrictions affects trams (thanks to @chem for reporting)
- Bugfix: Manual pedestrian traffic light states were not correctly handled
- Bugfix: Junction restrictions overlay did not show all restricted junctions

1.7.9, 08/22/2016
- In-game traffic light states are now correctly rendered when showing "yellow"
- Removed negative effects on public transport usage
- GUI: Traffic light states do not flicker anymore
- Performance improvements 

1.7.8, 08/18/2016:
- Bugfix: Cims sometimes got stuck (thanks to all reports and especially to @Thilawyn for providing a savegame)
- GUI: Improved traffic light arrow display
- Improved performance while saving

1.7.7, 08/16/2016:
- AI: Instead of walking long distances, citizens now use a car
- AI: Citizens will remember their last used mode of transport (e.g. they will not drive to work and come return by bus anymore)
- AI: Increased path-finding costs for traversing over restricted road segments
- Added "110" speed limit
- GUI: Windows are draggable
- GUI: Improved window scaling on lower resolutions
- Improved performance while saving

1.7.6, 08/14/2016:
- New feature: Players may now prohibit cims from crossing the street
- AI: Tuned randomization of lane changing behavior
- AI: Introduced path-finding costs for leaving main highway (should reduce amount of detours taken)
- UI: Clicking with the secondary mouse button now deselects the currently selected node/segment for all tools
- Added the possibility to connect train track lanes with the lane connector (as requested by @pilot.patrick93)
- Moved options from "Change lane arrows" to "Vehicle restrictions" tool
- Updated Russian translation
- Bugfix: AI: At specific junctions, vehicles were not obeying lane connections correctly (thanks to @Mariobro14 for pointing out this problem)
- Bugfix: AI: Path-finding costs for u-turns were not correctly calculated (thanks to @Mariobro14 for pointing out this problem)
- Bugfix: Vehicles were endlessly waiting for each other at junctions with certain priority sign configurations
- Bugfix: AI: Lane changing costs corrected

1.7.5, 08/07/2016:
- Bugfix: AI: Cims were using pocket cars whenever possible
- Bugfix: AI: Path-finding failures led to much less vehicles spawning
- Bugfix: AI: Lane selection at junctions with custom lane connection was not always working properly (e.g. for Network Extensions roads with middle lane)
- Bugfix: While editing a timed traffic light it could happen that the traffic light was deleted

1.7.4, 07/31/2016:
- AI: Switched from relative to absolute traffic density measurement
- AI: Tuned new parameters
- Bugfix: Activated/Disabled features were not loaded correctly
- Bugfix: AI: At specific junctions the lane changer did not work as intended
- Possible fix for OSX performance issues
- Code improvements
- Added French translations (thanks to @simon.royer007 for translating!)

1.7.3, 07/29/2016:
- Added the ability to enable/disable mod features (e.g. for performance reasons)
- Bugfix: Vehicle type determination was incorrect (fixed u-turning trams/trains, stuck vehicles)
- Bugfix: Clicking on a train/tram node with the lane connector tool led to an uncorrectable error (thanks to @noaccount for reporting this problem)
- Further code improvements

1.7.2, 07/26/2016:
- Optimized UI overlay performance

1.7.1, 07/24/2016:
- Reverted "Busses now may only ignore lane arrows if driving on a bus lane" for now
- Bugfix: Trains were not despawning if no path could be calculated
- Workaround for third-party issue: TM:PE now detects if the calculation of total vehicle length fails    

1.7.0, 07/23/2016:
- New feature: Traffic++ lane connector
- Busses now may only ignore lane arrows if driving on a bus lane
- Rewritten and simplified vehicle position tracking near timed traffic lights and priority signs for performance reasons
- Improved performance of priority sign rules
- AI: Cims now ignore junctions where pedestrian lights never change to green
- AI: Removed the need to define a lane changing probability 
- AI: Tweaked lane changing parameters
- AI: Highway rules are automatically disabled at complex junctions (= more than 1 incoming and more than 1 outgoing roads)
- Improved UI performance if overlays are deactivated
- Simulation accuracy now also controls time intervals between traffic measurements
- Added compatibility detection for the Rainfall mod
- Improved fault-tolerance of the load/save system
- Default wait-flow balance is set to 0.8
- Bugfix: Taxis were allowed to ignore lane arrows
- Bugfix: AI: Highway rules on left-hand traffic maps did not work the same as on right-hand traffic maps
- Bugfix: Upgrading a road segment next to a timed traffic light removed the traffic light leading to an inconsistent state (thanks to @ad.vissers for pointing out this problem)

1.6.22, 06/29/2016:
- AI: Taxis now may not ignore lane arrows and are using bus lanes whenever possible (thanks to @Cochy for pointing out this issue)
- AI: Busses may only ignore lane arrows while driving on a bus lane
- Bugfix: Traffic measurement at timed traffic lights was incorrect

1.6.22, 06/21/2016:
- Speed/vehicle restrictions may now be applied to all road segments between two junctions by holding the shift key
- Reworked how changes in the road network are recognized 
- Advanced Vehicle AI: Improved lane selection at junctions where bus lanes end
- Advanced Vehicle AI: Improved lane selection of busses
- Improved automatic pedestrian lights 
- Improved separate traffic lights: Traffic lights now control traffic lane-wise
- UI: Sensitivity slider is only available while adding/editing a step or while in test mode
- Bugfix: Lane selection on maps with left-hand traffic was incorrect
- Bugfix: While building in pause mode, changes in the road network were not always recognized causing vehicles to stop/despawn 
- Bugfix: Police cars off-duty were ignoring lane arrows
- Bugfix: If public transport stops were near a junction, trams/busses were not counted by timed traffic lights (many thanks to Filip for identifying this problem)
- Bugfix: Trains/Trams were sometimes ignoring timed traffic lights (many thanks to Filip for identifying this problem)
- Bugfix: Building roads with bus lanes caused garbage, bodies, etc. to pile up 

1.6.21, 06/14/2016:
- Bugfix: Too few cargo trains were spawning (thanks to @Scratch, @toruk_makto1, @Mr.Miyagi, @mottoh and @Syparo for pointing out this problem)       
- Bugfix: Vehicle restrictions did not work as expected (thanks to @nordlaser for pointing out this problem)

1.6.20, 06/11/2016:
- Bugfix: Priority signs were not working correctly (thanks to @mottoth, @Madgemade for pointing out this problem)

1.6.19, 06/11/2016
- Bugfix: Timed traffic lights UI not working as expected (thanks to @Madgemade for pointing out this problem)

1.6.18, 06/09/2016
- Updated for game patch 1.5.0-f4
- Improved performance of priority signs and timed traffic lights
- Players can now select elevated rail segments/nodes 
- Trams and trains now follow priority signs
- Improved UI behavior when setting up priority signs

1.6.17, 04/20/2016
- Hotfix for reported path-finding problems

1.6.16, 04/19/2016
- Updated for game patch 1.4.1-f2

1.6.15, 03/22/2016
- Updated for game path 1.4.0-f3
- Possible fix for crashes described by @cosminel1982
- Added traditional Chinese translation

1.6.14, 03/17/2016
- Bugfix: Cargo trucks did not obey vehicle restrictions (thanks to @ad.vissers for pointing out this problem)
- Bugfix: When Advanced AI was deactivated, u-turns did not have costs assigned

1.6.13, 03/16/2016
- Added Dutch translation
- The pedestrian light mode of a traffic light can now be switched back to automatic
- Vehicles approaching a different speed limit change their speed more gradually
- The size of signs and symbols in the overlay is determined by screen resolution height, not by width
- Path-finding: Performance improvements
- Path-finding: Fine-tuned lane changing behaviour
- Bugfix: After loading another savegame, timed traffic lights stopped working for a certain time
- Bugfix: Lane speed calculation corrected

1.6.12, 03/03/2016
- Improved memory usage
- Bugfix: Adding/removing junctions to/from existing timed traffic lights did not work (thanks to @nieksen for pointing out this problem)
- Bugfix: Separate timed traffic lights were sometimes not saved (thanks to @nieksen for pointing out this problem)
- Bugfix: Fixed an initialization error (thanks to @GordonDry for pointing out this problem)

1.6.11, 03/03/2016
- Added Chinese translation
- By pressing "Page up"/"Page down" you can now switch between traffic and default map view
- Size of information icons and signs is now based on your screen resolution
- UI code refactored 

1.6.10, 03/02/2016
- Additional controls for vehicle restrictions added
- Bugfix: Clicking on a Traffic Manager overlay resulted in vanilla game components (e.g. houses, vehicles) being activated 

1.6.9, 03/02/2016
- Updated for game patch 1.3.2-f1

1.6.8, 03/01/2016
- Path-finding: Major performance improvements
- Updated Japanese translation (thanks to @Akira Ishizaki for translating!)
- Added Spanish translation

1.6.7, 02/27/2016
- Tuned AI parameters
- Improved traffic density measurements
- Improved lane changing near junctions: Reintroduced costs for lane changing before junctions
- Improved vehicle behavior near blocked roads (e.g. while a building is burning)
- Bugfix: Automatic pedestrian lights for outgoing one-ways fixed
- Bugfix: U-turns did not have appropriate costs assigned
- Bugfix: The time span between AI traffic measurements was too high

1.6.6, 02/27/2016
- It should now be easier to select segment ends in order to change lane arrows.
- Priority signs now cannot be setup at outgoing one-ways.
- Updated French translation (thanks to @simon.royer007 for translating!)
- Updated Polish translation (thanks to @Krzychu1245 for translating!)
- Updated Portuguese translation (thanks to @igordeeoliveira for translating!)
- Updated Russian translation (thanks to @FireGames for translating!)
- Bugfix: U-turning vehicles were not obeying the correct directional traffic light (thanks to @t1a2l for pointing out this problem)

1.6.5, 02/24/2016
- Added despawning setting to options dialog
- Improved detection of Traffic++ V2

1.6.4, 02/23/2016
- Minor performance improvements
- Bugfix: Path-finding calculated erroneous traffic density values 
- Bugfix: Cims left the bus just to hop on a bus of the same line again (thanks to @kamzik911 for pointing out this problem)
- Bugfix: Despawn control did not work (thanks to @xXHistoricalxDemoXx for pointing out this problem)
- Bugfix: State of new settings was not displayed corretly (thanks to @Lord_Assaultーさま for pointing out this problem)
- Bugfix: Default settings for vehicle restrictions on bus lanes corrected
- Bugfix: Pedestrian lights at railway junctions fixed (they are still invisible but are derived from the car traffic light state automatically)

1.6.3, 02/22/2016
- Bugfix: Using the "Old Town" policy led to vehicles not spawning.
- Bugfix: Planes, cargo trains and ship were sometimes not arriving
- Bugfix: Trams are not doing u-turns anymore

1.6.2, 02/20/2016
- Trams are now obeying speed limits (thanks to @Clausewitz for pointing out the issue)
- Bugfix: Clear traffic sometimes throwed an error
- Bugfix: Vehicle restrctions did not work as expected (thanks to @[Delta ²k5] for pointing out this problem) 
- Bugfix: Transition of automatic pedestrian lights fixed

1.6.1, 02/20/2016
- Improved performance
- Bugfix: Fixed UI issues
- Modifying mod options through the main menu now gives an annoying warning message instead of a blank page.

1.6.0, 02/18/2016
- New feature: Separate traffic lights for different vehicle types
- New feature: Vehicle restrictions
- Snowfall compatibility
- Better handling of vehicle bans
- Improved the method for calculating lane traffic densities
- Ambulances, fire trucks and police cars on duty are now ignoring lane arrows
- Timed traffic lights may now be setup at arbitrary nodes on railway tracks
- Reckless drivers now do not enter railroad crossings if the barrier is down
- Option dialog is disabled if accessed through the main menu
- Performance optimizations
- Advanced Vehicle AI: Improved lane spreading
- The option "Vehicles may enter blocked junctions" may now be defined for each junction separately
- Vehicles going straight may now change lanes at junctions
- Vehicles may now perform u-turns at junctions that have an appropriate lane arrow configuration
- Road conditions (snow, maintenance state) may now have a higher impact on vehicle speed (see "Options" menu)
- Emergency vehicles on duty now always aim for the the fastest route
- Bugfix: Path-finding costs for crossing a junction fixed
- Bugfix: Vehicle detection at timed traffic lights did not work as expected
- Bugfix: Not all valid traffic light arrow modes were reachable

1.5.2, 02/01/2016
- Traffic lights may now be added to/removed from underground junctions
- Traffic lights may now be setup at *some* points of railway tracks (there seems to be a game-internal bug that prevents selecting arbitrary railway nodes)
- Display of priority signs, speed limits and timed traffic lights may now be toggled via the options dialog
- Bugfix: Reckless driving does not apply for trains (thanks to @GordonDry for pointing out this problem)
- Bugfix: Manual traffic lights were not working (thanks to @Mas71 for pointing out this problem)
- Bugfix: Pedestrians were ignoring timed traffic lights (thanks to @Hannes8910 for pointing out this problem)
- Bugfix: Sometimes speed limits were not saved (thanks to @cca_mikeman for pointing out this problem) 

1.5.1, 01/31/2016
- Trains are now following speed limits

1.5.0, 01/30/2016
- New feature: Speed restrictions (as requested by @Gfurst)
- AI: Parameters tuned
- Code improvements
- Lane arrow changer window is now positioned near the edited junction (as requested by @GordonDry)
- Bugfix: Flowing/Waiting vehicles count corrected

1.4.9, 01/27/2016
- Junctions may now be added to/removed from timed traffic lights after they are created
- When viewing/moving a timed step, the displayed/moved step is now highlighted (thanks to Joe for this idea)
- Performance improvements
- Bugfix (AI): Fixed a division by zero error (thanks to @GordonDry for pointing out this problem)
- Bugfix (AI): Near highway exits vehicles tended to use the outermost lane (thanks to @Zake for pointing out this problem)
- Bugfix: Some lane arrows disappeared on maps using left-hand traffic systems (thanks to @Mas71 for pointing out this problem)
- Bugfix: In lane arrow edit mode, the order of arrows was sometimes incorrect (thanks to @Glowstrontium for pointing out this problem)
- Bugfix: Lane merging in left-hand traffic systems fixed
- Bugfix: Turning priority roads fixed (thanks to @GordonDry for pointing out this problem)

1.4.8, 01/25/2016
- AI: Parameters have been tuned
- AI: Added traffic density measurements
- Performance improvements
- Added translation to Polish (thanks to @Krzychu1245 for working on this!)
- Added translation to Russian (thanks to @FireGames for working on this!)
- Bugfix: After removing a timed or manual light the traffic light was deleted (thanks to @Mas71 for pointing out this problem)
- Bugfix: Segment geometries were not always calculated
- Bugfix: In highway rule mode, lane arrows sometimes flickered 
- Bugfix: Some traffic light arrows were sometimes not selectable 
 
1.4.7, 01/22/2016
- Added translation to Portuguese (thanks to @igordeeoliveira for working on this!) 
- Reduced mean size of files can become quite big (thanks to @GordonDry for reporting this problem)
- Bugfix: Freight ships/trains were not coming in (thanks to @Mas71 and @clus for reporting this problem)
- Bugfix: The toggle "Vehicles may enter blocked junctions" did not work properly (thanks for @exxonic for reporting this problem)
- Bugfix: If a timed traffic light is being edited the segment geometry information is not updated (thanks to @GordonDry for reporting this problem)

1.4.6, 01/22/2016
- Running average lane speeds are measured now
- Minor fixes

1.4.5, 01/22/2016
- The option "Vehicles may enter blocked junctions" may now be defined for each junction separately
- Bugfix: A deadlock in the path-finding is fixed
- Bugfix: Small timed light sensitivity values (< 0.1) were not saved correctly 
- Bugfix: Timed traffic lights were not working for some players
- Refactored segment geometry calculation

1.4.4, 01/21/2016
- Added localization support

1.4.3, 01/20/2016
- Several performance improvements
- Improved calculation of segment geometries
- Improved load balancing
- Police cars, ambulances, fire trucks and hearses are now also controlled by the AI
- Bugfix: Vehicles did not always take the shortest path
- Bugfix: Vehicles disappeared after deleting/upgrading a road segment
- Bugfix: Fixed an error in path-finding cost calculation
- Bugfix: Outgoing roads were treated as ingoing roads when highway rules were activated

1.4.2, 01/16/2016
- Several major performance improvements (thanks to @sci302 for pointing out those issues)
- Improved the way traffic lights are saved/loaded
- Lane-wise traffic density is only measured if Advanced AI is activated
- Bugfix: AI did not consider speed limits/road types during path calculation (thanks to @bhanhart, @sa62039 for pointing out this problem)
- Connecting a city road to a highway road that does not supply enough lanes for merging leads to behavior people do not understand (see manual). Option added to disable highway rules.  
- Bugfix: Vehicles were stopping in front of green traffic lights
- Bugfix: Stop/Yield signs were not working properly (thanks to @GordonDry, @Glowstrontium for pointing out this problem)
- Bugfix: Cargo trucks were ignoring the "Heavy ban" policy, they should do now (thanks to @Scratch for pointing out this problem)

1.4.1, 01/15/2016
- Bugfix: Path-finding near junctions fixed

1.4.0, 01/15/2016
- Introducing Advanced Vehicle AI (disabled by default! Go to "Options" and enable it if you want to use it.)
- Bugfix: Traffic lights were popping up in the middle of roads
- Bugfix: Fixed the lane changer for left-hand traffic systems (thanks to @Phishie for pointing out this problem)
- Bugfix: Traffic lights on invalid nodes are not saved anymore 

1.3.24, 01/13/2016
- Improved handling of priority signs
- Priority signs: After adding two main road signs the next offered sign is a yield sign
- Priority signs: Vehicles now should notice earlier that they can enter a junction
- Removed the legacy XML file save system
- Invalid (not created) lanes are not saved/loaded anymore
- Added a configuration option that allows vehicles to enter blocked junctions
- Bugfix: Some priority signs were not saved
- Bugfix: Priority signs on deleted segments are now deleted too
- Bugfix: Lane arrows on removed lanes are now removed too
- Bugfix: Adding a priority sign to a junction having more than one main sign creates a yield sign (thanks to @GordonDry for pointing out this problem)
- Bugfix: If reckless driving was set to "The Holy City (0 %)", vehicles blocked intersections with traffic light.
- Bugfix: Traffic light arrow modes were sometimes not correctly saved  

1.3.23, 01/09/2016
- Bugfix: Corrected an issue where toggled traffic lights would not be saved/loaded correctly (thanks to @Jeffrios and @AOD_War_2g for pointing out this problem)
- Option added to forget all toggled traffic lights

1.3.22, 01/08/2016
- Added an option allowing busses to ignore lane arrows
- Added an option to display nodes and segments

1.3.21, 01/06/2016
- New feature: Traffic Sensitivity Tuning
- UI improvements: When adding a new step to a timed traffic light the lights are inverted.
- Timed traffic light status symbols should now be less annoying 
- Bugfix: Deletion of junctions that were members of a traffic light group is now handled correctly 

1.3.20, 01/04/2016
- Bugfix: Timed traffic lights are not saved correctly after upgrading a road nearby
- UI improvements
- New feature: Reckless driving 

1.3.19, 01/04/2016
- Timed traffic lights: Absolute minimum time changed to 1
- Timed traffic lights: Velocity of vehicles is being measured to detect traffic jams
- Improved traffic flow measurement
- Improved path finding: Cims may now choose their lanes more independently
- Bugfix: Upgrading a road resets the traffic light arrow mode

1.3.18, 01/03/2016
- Provided a fix for unconnected junctions caused by other mods
- Crosswalk feature removed. If you need to add/remove crosswalks please use the "Crossings" mod.
- UI improvements: You can now switch between activated timed traffic lights without clicking on the menu button again

1.3.17, 01/03/2016
- Bugfix: Timed traffic lights cannot be added again after removal, toggling traffic lights does not work (thanks to @Fabrice, @ChakyHH, @sensual.heathen for pointing out this problem)
- Bugfix: After using the "Manual traffic lights" option, toggling lights does not work (thanks to @Timso113 for pointing out this problem)

1.3.16, 01/03/2016
- Bugfix: Traffic light settings on roads of the Network Extensions mods are not saved (thanks to @Scarface, @martintech and @Sonic for pointing out this problem)
- Improved save data management 

1.3.15, 01/02/2016
- Simulation accuracy (and thus performance) is now controllable through the game options dialog
- Bugfix: Vehicles on a priority road sometimes stop without an obvious reason

1.3.14, 01/01/2016
- Improved performance
- UI: Non-timed traffic lights are now automatically removed when adding priority signs to a junction
- Adjusted the adaptive traffic light decision formula (vehicle lengths are considered now)
- Traffic two road segments in front of a timed traffic light is being measured now  

1.3.13, 01/01/2016
- Bugfix: Lane arrows are not correctly translated into path finding decisions (thanks to @bvoice360 for pointing out this problem)
- Bugfix: Priority signs are sometimes undeletable (thank to @Blackwolf for pointing out this problem) 
- Bugfix: Errors occur when other mods without namespace definitions are loaded (thanks to @Arch Angel for pointing out this problem)
- Connecting a new road segment to a junction that already has priority signs now allows modification of the new priority sign

1.3.12, 12/30/2015
- Bugfix: Priority signs are not editable (thanks to @ningcaohan for pointing out this problem)

1.3.11, 12/30/2015
- Road segments next to a timed traffic light may now be deleted/upgraded/added without leading to deletion of the light
- Priority signs and Timed traffic light state symbols are now visible as soon as the menu is opened

1.3.10, 12/29/2015
- Fixed an issue where timed traffic light groups were not deleted after deleting an adjacent segment

1.3.9, 12/29/2015
- Introduced information icons for timed traffic lights
- Mod is now compatible with "Improved AI" (Lane changer is deactivated if "Improved AI" is active)

1.3.8, 12/29/2015
- Articulated busses are now simulated correctly (thanks to @nieksen for pointing out this problem)
- UI improvements

1.3.7, 12/28/2015
- When setting up a new timed traffic light, yellow lights from the real-world state are not taken over
- When loading another save game via the escape menu, Traffic Manager does not crash
- When loading another save game via the escape menu, Traffic++ detection works as intended
- Lane arrows are saved correctly

1.3.6, 12/28/2015
- Bugfix: wrong flow value taken when comparing flowing vehicles
- Forced node rendering after modifying a crosswalk

1.3.5, 12/28/2015
- Fixed pedestrian traffic Lights (thanks to @Glowstrontium for pointing out this problem)
- Better fix for: Deleting a segment with a timed traffic light does not cause a NullReferenceException
- Adjusted the comparison between flowing (green light) and waiting (red light) traffic

1.3.4, 12/27/2015
- Better traffic jam handling

1.3.3, 12/27/2015
- (Temporary) hotfix: Deleting a segment with a timed traffic light does not cause a NullReferenceException
- If priority signs are located behind the camera they are not rendered anymore

1.3.2, 12/27/2015
- Priority signs are persistently visible when Traffic Manager is in "Add priority sign" mode
- Synchronized traffic light rendering: In-game Traffic lights display the correct color (Thanks to @Fabrice for pointing out this problem)
- Traffic lights switch between green, yellow and red. Not only between green and red.
- UI tool tips are more explanatory and are shown longer.

1.3.1, 12/26/2015
- Minimum time units may be zero now
- Timed traffic lights of deleted/modified junctions get properly disposed

1.3.0, 12/25/2015
- **Adaptive Timed Traffic Lights** (automatically adjusted based on traffic amount)

1.2.0 (iMarbot)
- Updated for 1.2.2-f2 game patch.

# Current features

- Add/Remove traffic lights
- Adaptive timed traffic lights
- Add priority signs
- Change lane arrows
- Connect individual lanes with each other
- Add/Remove crosswalks
- Manually control traffic lights
- Timed traffic lights
- Clear traffic
- No despawn

# Todo list

- I would like to investigate why yellow traffic lights sometimes are not properly rendered.
- I would like to implement traffic light templates so that you would not need to manually set up individual steps for common junction patterns.
- Stop signs should be more useful. Drivers should act more realistically/confident when stop/yield signs and traffic jams meet together.
- Adaptive Timed Traffic Lights: Currently only vehicles on the road segment next to the junction are being measured. I would like to expand the traffic measurement to 2 segments.
- When switching between control modes, the UI starts to ignore user mouse input. This is annoying. I will hopefully fix that.
- We could measure if there is traffic backing up after a timed traffic light. If it is the case (that is: cars having a green light do not move) the next timed step could be activated.
- There are still some issues with crossings and pedestrain crossing lights (missing textures, double crosswalks). Let's see what can be done.
- For new users it takes some time to understand how the mod works. Having something like a (video) manual would be great. Or just a better UI. 

# Upcoming changes

- Code optimization & refactoring
- Timed Traffic Light Templates (ready-to-use directional traffic light patterns)
