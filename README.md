<p align="center"><img src="https://user-images.githubusercontent.com/16494272/59316295-ee189d00-8c7a-11e9-93a2-266292b6f3e9.png" width="740" height="232" /></p>
<p align="center">A mod for <strong>Cities: Skylines</strong> that gives you more control over road and rail traffic in your city<br /><br /></p>
<p align="center"><a href="https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252">Steam Workshop</a> • <a href="https://discord.gg/faKUnST">Discord Guild</a> • <a href="https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki">User Guide</a> • <a href="https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Report-a-Bug">Report a Bug</a><br /></p>
<p align="center"><a href="https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252"><img src="https://img.shields.io/endpoint.svg?url=https://shieldsio-steam-workshop.jross.me/583429740" /></a> <a href="https://store.steampowered.com/app/255710/Cities_Skylines/"><img src="https://img.shields.io/badge/Game%20Version-1.12.3--f2-brightgreen.svg"></a> <a href="https://discord.gg/faKUnST"><img src="https://img.shields.io/discord/545065285862948894.svg?logo=discord&logoColor=F5F5F5" /></a> <a href="https://crowdin.com/project/tmpe"><img src="https://badges.crowdin.net/tmpe/localized.svg"></a> <a href="https://ci.appveyor.com/project/krzychu124/cities-skylines-traffic-manager-president-edition/branch/master"><img src="https://ci.appveyor.com/api/projects/status/dehkvuxk8b3h66e7/branch/master?svg=true" /></a></p>

## Notices

* Use [Broken Node Detector](https://steamcommunity.com/sharedfiles/filedetails/?id=1777173984) to find and fix traffic despawning issues and a few other game bugs
* Other problems? See: [Troubleshooting Guide](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Troubleshooting)

## Releases

Official releases:
* [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252) (fully tested releases)
* [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141) (latest beta test releases)
* [Download Binaries](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/releases) (for non-Steam users)
* [Installation Guide](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Installation) (for all users)

#### TM:PE V11 LABS [11.1.0](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/compare/11.0...11.1.0), 03/02/2020

- Added: Quick setup of priority roads (`Ctrl+Click junction`, `Shift+Ctrl+Click road`) (#621, #541, #542, #568, #577, #7)
- Added: `Delete` key resets junction restrictions for selected junction (#639, #623, #568, #6)
- Added: "Reset" button and `Delete` key to reset lane arrows for a segment (#638, #632, #623, #568, #41)
- Improved: Much better lane connectors interaction model (#543, #635, #625, #626, #41)
- Improved: Use guide manager for less obtrusive in-game warnings/hints (#653, #660, #593)
- Improved: Vastly improved in-game hotloading support (#640, #211)
- Improved: Centralised versioning in to `SharedAssemblyInfo.cs` (#680, #678, #649)
- Updated: Translations - Dutch (thanks Headspike!) (#660, #631)
- Updated: Translations - Turkish - Tayfun [Typhoon] (thanks Koopr) (#660, #631)
- Updated: Translations - Chinese Simplified - Golden (thanks goldenjin!) (#660, #631)
- Updated: Translations - Portuguese - BlackScout (thanks BS_BlackScout!) (#660, #631)
- Updated: Translations - Spanish (thanks Aimarekin!) (#660, #631)
- Updated: Translations - English (thanks kian.zarrin!) (#660, #631)
- Fixed: Vehicles should not stop at Yield signs (#662, #655, #650)
- Meta: New WIP website: https://tmpe.me (#642, #643)

#### TM:PE V11 STABLE [11.0](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/compare/10.21.1...11.0), 03/02/2020

- Contains ~100 improvements from TM:PE v11 ALPHA versions, including:
    - Timed traffic lights: Add default sequence (Ctrl+Click a junction)
    - Lane arrows: Turning lanes (Ctrl+Click a junction, or Alt+Click a segment)
    - Vanilla traffic lights: Remove or Disable auto-placed traffic lights (buttons in mod options)
    - New [languages](https://crowdin.com/project/tmpe): Hungarian, Turkish, Ukrainian; all other languages updated!
    - Migration to Harmony for improved compatibility
- Improved: Better segment hovering when mouse near segment (thanks kianzarrin!) (#624, #576)
- Improved: Better segment hovering when mouse on node (thanks kianzarrin!) (#615, #538, #594, #616, #576)
- Fixed: Lane arrow tool sometimes selects wrong node (thanks kianzarrin!) (#616)
- Fixed: Show error dialog can get caught in loop (thanks kianzarrin!) (#594)
- Fixed: Junction Manager not resetting on level unload (thanks kianzarrin!) (#637, #636)
- Fixed: Stay in lane always assumed segment0 exists (thans kianzarrin!) (#619, #618)
- Updated: Added 2 x Traffic Manager Plus and 1 x Traffic Manager as incompatible (#627)
- Updated: Added 'Trees Respiration' mod as incompatible (depends on load order) (#614, #611, #563)
- Updated: Replaced imports with fully qualified alphabetically sorted imports (#620)
- Updated: Organised resource images in to folders (#641)
- Meta: Old STABLE workshop page (LinuxFan - v10.20) is now obsolete and no longer maintained
- Meta: Renamed workshop pages; LABS -> V11 STABLE, and ALPHA -> V11 LABS

See [Full Changelog](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/blob/master/CHANGELOG.md) for details of earlier releases and LABS releases.

## Support Policy

Our team is happy to support you if:
- You are using the latest version of **TM:PE v11 STABLE** or **TM:PE v11 LABS**
- You are using a properly purchased and latest version of Cities: Skylines
- You provide useful information when [reporting a bug](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Report-a-Bug)

We will _not_ provide support if:
- You are using a pirated or old version of Cities: Skylines
- You are using an older version of the mod

TM:PE is only tested on and updated for the latest version of Cities: Skylines.

## Contributing

We welcome contributions from the community! See: [Contributing Guide](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Contributing)

## License

[MIT License](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/blob/master/LICENSE) (free, open source)
