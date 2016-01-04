# Cities: Skylines - Traffic Manager: *Traffic President Edition*
A work-in-progress modification for **Cities: Skylines** to add additional road traffic control

# Changelog
1.3.19, 01/04/2016
- Timed traffic lights: Absolute minimum time changed to 1
- Timed traffic lights: Velocity of vehicles is being measured to detect traffic jams
- Improved traffic flow measurement
- Improved path finding: Cims may now choose their lanes more independently

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
- Change lanes
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
