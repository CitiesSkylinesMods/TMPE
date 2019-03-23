# Traffic Manager: *President Edition* [![Discord](https://img.shields.io/discord/545065285862948894.svg)](https://discord.gg/faKUnST) [![Build status](https://ci.appveyor.com/api/projects/status/dehkvuxk8b3h66e7/branch/master?svg=true)](https://ci.appveyor.com/project/krzychu124/cities-skylines-traffic-manager-president-edition/branch/master)

A mod for **Cities: Skylines** that gives you more control over road and rail traffic in your city.

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252) • [Installation](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Installation) • [User Guide](http://www.viathinksoft.de/tmpe/wiki) • [Issue Tracker](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/issues) • [Report a Bug](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Report-a-Bug)

# Features

* Timed traffic lights
* Change lane arrows
* Edit lane connections
* Add priority signs
* Junction restrictions
    * Toggle u-turns
    * Allow "turn on red"
    * Enter blocked junctions
    * Toggle pedestrian crossings
* Vehicle restrictions
    * For roads and trains!
* Customise speed limits
* Toggle despawn
* Clear traffic, stuck cims, etc.

# Changelog
### [10.17](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/compare/1.10.16...10.17), 23/03/2019
- Introduced new versioning scheme (10.17 instead of 1.10.17)
- Synchronized code and version with stable version
- Updated russian translation (thanks to @vitalii201 for translating) (#207)
- Updated list of incompatible mods (#115)
- Removed stable version from list of incompatible mods (#168)
- Turn-on-red can now be toggled for unpreferred turns between one-ways
- Improved train behavior at shunts: Trains now prefer to stay on their track (#230)
- Fixed and optimized lane selection for u-turns and at dead ends (#101)
- Parking AI: Improved public transport (PT) usage patterns, mixed car/PT paths are now possible  (#218)
- Bugfix: Parking AI: Tourist cars despawn because they assume they are at an outside connection (#218)
- Bugfix: Parking AI: Return path calculation did not accept beautification segments (#218)
- Bugfix: Parking AI: Cars/Citizens waiting for a path might jump around (#218)
- Bugfix: Vanilla lane randomization does not work as intended at highway transitions (#112)
- Bugfix: Vehicles change lanes at tollbooths (#225)
- Bugfix: Path-finding: Array index is out of range due to a race condition (#221)
- Bugfix: Citizen not found errors when using walking tours (#219)
- Bugfix: Timed light indicator only visible when any timed light node is selected (#222)

See [Full Changelog](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/blob/master/CHANGELOG.md) for details of earlier releases.

# Contributing

We welcome contributions from the community!

Contact us:

* [Issue tracker](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/issues)
* [Discord (chat)](https://discord.gg/faKUnST)
* [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

# License

[MIT License](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/blob/master/LICENSE) (open source)
