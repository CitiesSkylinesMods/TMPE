# Cities: Skylines - Traffic Manager: *Traffic President Edition*
A work-in-progress modification for **Cities: Skylines** to add additional road traffic control

# Changelog
1.3.11, 12/30/2015 (Victor-Philipp Negoescu)
- Road segments next to a timed traffic light may now be deleted/upgraded/added without leading to deletion of the light
- Priority signs and Timed traffic light state symbols are now visible as soon as the menu is opened

1.3.10, 12/29/2015 (Victor-Philipp Negoescu)
- Fixed an issue where timed traffic light groups were not deleted after deleting an adjacent segment

1.3.9, 12/29/2015 (Victor-Philipp Negoescu)
- Introduced information icons for timed traffic lights
- Mod is now compatible with "Improved AI" (Lane changer is deactivated if "Improved AI" is active)

1.3.8, 12/29/2015 (Victor-Philipp Negoescu)
- Articulated busses are now simulated correctly (thanks to @nieksen for pointing out this problem)
- UI improvements

1.3.7, 12/28/2015 (Victor-Philipp Negoescu)
- When setting up a new timed traffic light, yellow lights from the real-world state are not taken over
- When loading another save game via the escape menu, Traffic Manager does not crash
- When loading another save game via the escape menu, Traffic++ detection works as intended
- Lane arrows are saved correctly

1.3.6, 12/28/2015 (Victor-Philipp Negoescu)
- Bugfix: wrong flow value taken when comparing flowing vehicles
- Forced node rendering after modifying a crosswalk

1.3.5, 12/28/2015 (Victor-Philipp Negoescu)
- Fixed pedestrian traffic Lights (thanks to @Glowstrontium for pointing out this problem)
- Better fix for: Deleting a segment with a timed traffic light does not cause a NullReferenceException
- Adjusted the comparison between flowing (green light) and waiting (red light) traffic

1.3.4, 12/27/2015 (Victor-Philipp Negoescu)
- Better traffic jam handling

1.3.3, 12/27/2015 (Victor-Philipp Negoescu)
- (Temporary) hotfix: Deleting a segment with a timed traffic light does not cause a NullReferenceException
- If priority signs are located behind the camera they are not rendered anymore

1.3.2, 12/27/2015 (Victor-Philipp Negoescu)
- Priority signs are persistently visible when Traffic Manager is in "Add priority sign" mode
- Synchronized traffic light rendering: In-game Traffic lights display the correct color (Thanks to @Fabrice for pointing out this problem)
- Traffic lights switch between green, yellow and red. Not only between green and red.
- UI tool tips are more explanatory and are shown longer.

1.3.1, 12/26/2015 (Victor-Philipp Negoescu)
- Minimum time units may be zero now
- Timed traffic lights of deleted/modified junctions get properly disposed

1.3.0, 12/25/2015 (Victor-Philipp Negoescu)
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
