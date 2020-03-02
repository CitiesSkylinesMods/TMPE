<p align="center">
    <img src="https://user-images.githubusercontent.com/16494272/59316295-ee189d00-8c7a-11e9-93a2-266292b6f3e9.png" width="740" height="232" />
</p>
<p align="center">A mod for <strong>Cities: Skylines</strong> that gives you more control over road and rail traffic in your city.<br /><br /></p>
<p align="center">
    <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252">Steam Workshop</a> •
    <a href="https://discord.gg/faKUnST">Discord Chat</a> •
    <a href="https://github.com/CitiesSkylinesMods/TMPE/wiki">User Guide</a> •
    <a href="https://github.com/CitiesSkylinesMods/TMPE/wiki/Report-a-Bug">Report a Bug</a><br />
</p>
<p align="center">
    <a href="https://store.steampowered.com/app/255710/Cities_Skylines/"><img src="https://img.shields.io/static/v1?label=cities:%20skylines&message=v1.12.3-f2&color=01ABF8&logo=unity" /></a>
    <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252"><img src="https://img.shields.io/github/v/release/CitiesSkylinesMods/TMPE?label=stable&color=7cc17b&logo=steam&logoColor=F5F5F5" /></a>
    <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141"><img src="https://img.shields.io/github/v/release/CitiesSkylinesMods/TMPE?include_prereleases&label=labs&color=f7b73c&logo=steam&logoColor=F5F5F5" /></a>
    <a href="https://github.com/CitiesSkylinesMods/TMPE/releases/latest"><img src="https://img.shields.io/github/v/release/CitiesSkylinesMods/TMPE?label=origin&color=F56C2D&logo=origin&logoColor=F56C2D" /></a>
    <a href="https://github.com/CitiesSkylinesMods/TMPE/releases"><img src="https://img.shields.io/github/v/release/CitiesSkylinesMods/TMPE?label=downloads&include_prereleases&logo=ipfs&logoColor=F5F5F5" /></a>
    <a href="https://discord.gg/faKUnST"><img src="https://img.shields.io/discord/545065285862948894?color=7289DA&label=chat&logo=discord" /></a>
</p>
<p align="center">
    <a href="https://ci.appveyor.com/project/krzychu124/tmpe/branch/master"><img src="https://img.shields.io/appveyor/build/krzychu124/TMPE/master?label=appveyor:master&logo=appveyor&logoColor=F5F5F5" /></a>
    <a href="https://github.com/CitiesSkylinesMods/TMPE/pulls"><img src="https://img.shields.io/github/issues-pr/CitiesSkylinesMods/TMPE?color=brightgreen&logo=github&logoColor=F5F5F5" /></a>
    <a href="https://crowdin.com/project/tmpe"><img src="https://badges.crowdin.net/tmpe/localized.svg" /></a>
    <a href="https://github.com/CitiesSkylinesMods/TMPE/blob/11.0/LICENSE"><img src="https://img.shields.io/github/license/CitiesSkylinesMods/TMPE?color=brightgreen&label=open%20source&logoColor=F5F5F5" /></a>
</p>

## Notices

* Use [Broken Node Detector](https://steamcommunity.com/sharedfiles/filedetails/?id=1777173984) to find and fix traffic despawning issues and a few other game bugs
* Other problems? See: [Troubleshooting Guide](https://github.com/CitiesSkylinesMods/TMPE/wiki/Troubleshooting)

## Releases

Official releases:

* [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252) (fully tested releases)
* [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141) (latest beta test releases)
* [Download Binaries](https://github.com/CitiesSkylinesMods/TMPE/releases) (for non-Steam users)
* [Installation Guide](https://github.com/CitiesSkylinesMods/TMPE/wiki/Installation) (for all users)

#### TM:PE V[11.1.0](https://github.com/CitiesSkylinesMods/TMPE/compare/11.0...11.1.0) STABLE, 29/02/2020

- Added: Quick setup of priority roads (`Ctrl+Click junction`, `Shift+Ctrl+Click road`) (#621, #541, #542, #568, #577, #7)
- Added: `Delete` key resets junction restrictions for selected junction (#639, #623, #568, #6)
- Added: "Reset" button and `Delete` key resets lane arrows for a segment (#638, #632, #623, #568, #41)
- Improved: Much better lane connectors interaction model (#543, #635, #625, #626, #41)
- Improved: Use guide manager for less obtrusive in-game warnings/hints (#653, #660, #593)
- Improved: Vastly improved in-game hotloading support (#640, #211)
- Improved: Centralised versioning in to `SharedAssemblyInfo.cs` (#680, #678, #649)
- Updated: Translations - Dutch (thanks Headspike!) (#660, #631)
- Updated: Translations - Turkish (thanks Tayfun [Typhoon] / Koopr) (#660, #631)
- Updated: Translations - Chinese Simplified (thanks Golden / goldenjin!) (#660, #631)
- Updated: Translations - Portuguese (thanks BlackScout / BS_BlackScout!) (#660, #631)
- Updated: Translations - Spanish (thanks Aimarekin!) (#660, #631)
- Updated: Translations - English (thanks kian.zarrin!) (#660, #631)
- Fixed: Vehicles should not stop at Yield signs (#662, #655, #650)
- Meta: New WIP website: https://tmpe.me (#642, #643)
- Steam: [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE V[11.1.2](https://github.com/CitiesSkylinesMods/TMPE/compare/11.1.1-hotfix1...11.1.2) LABS, 02/03/2020

- Fixed: One-click traffic lights wrong way on RHT maps, murdering pedestrians (#770, #769, #690)

#### TM:PE V[11.1.1-hotfix1](https://github.com/CitiesSkylinesMods/TMPE/compare/11.1.1...11.1.1-hotfix1) LABS, 01/03/2020

- Fixed: Vehicles stopping at Yield signs (#761, #756)
- Fixed: Missing despawn buttons on cim and vehicle info panels (#765, #763, #759)
- Fixed: Info panel not closing after despawning a cim or tourist (#765)
- Fixed: Faulty UI on tourist despawn button (#765)
- Updated: Docs - Reference paths for EA Origin deployed game (thanks DannyDannyDan) ( #751)

#### TM:PE V[11.1.1](https://github.com/CitiesSkylinesMods/TMPE/compare/11.1.0...11.1.1) LABS, 29/02/2020

- Added: The `Simulation Accuracy` option has been revived! (#742, #707)
- Added: `Shift` key applies a setting to entire route + lane highlight (#138, #721, #709, #708, #667, #388, #33)
- Added: Lane highlighting - Vehicle Restrictions tool (#721, #681, #667, #42, #33)
- Added: Lane highlighting - Parking Restrictions tool (#708, #702, #667, #47, #33)
- Added: Lane highlighting - Speed Limits tool (#709, #682, #667, #388, #84, #52, #33)
- Added: Button to reset speed limit to default added to speeds palette (#709)
- Added: UI scaling slider in mod options "General" tab (#656)
- Removed: Drag along road to set speed limits, due to performance issues (#388)
- Fixed: Only selected vehicle restriction, not all, should be applied to route (#721)
- Fixed: Lane connector can't make U-turns on roads with monorails (#293)
- Fixed: Lane connectors could connect tracks disconnected by `MaxTurnAngle` (#684, #649)
- Fixed: Wrong texture paths for timed traffic lights (thanks t1a2l for reporting!) (#732, #704, #714)
- Fixed: Bug in guide manager that activated guide when trying to deactivate (#729)
- Fixed: Double setting of lane speeds on game load, and debug log spamming (#736, #735)
- Fixed: Scrollbar position corrected in mod options (#722, #742)
- Fixed: Vehicle Restrictions error: `HashSet have been modified` (#746, #744, #721)
- Improved: Cleaned up UI panels in Vehicle Restrictions and Speed Limits tools (#721, #709, #657)
- Improved: Toolbar UI code overhauled, updated and polished (#656, #523)
- Improved: Compatibility with CSUR Reloaded (#684, #649, #687, CSURToolBox#1, CSURToolBox#2)
- Improved: Organised lane markers/highlighters in to distinct classes (#701, #630)
- Improved: Better reference `.dll` hint paths for Mac and Windows developers (#664, #663)
- Improved: Faster and more reliable hot-reloads of dev builds (#725, #717, #718)
- Improved: Reduced memalloc and gc in SpeedLimitsManager.OnBeforeData() logging (#753)
- Updated: Translations - Chinese Simplified (thanks 田七不甜 / TianQiBuTian) (#723)
- Updated: Translations - Chinese Traditional (thanks jrthsr700tmax) (#723)
- Updated: Translations - Dutch (thanks Headspike) (#723, #742)
- Updated: Translations - English (thanks kian.zarrin, Dmytro Lytovchenko / kvakvs) (#723, #742)
- Updated: Translations - Hungarian (thanks Krisztián Melich / StummeH) (#742)
- Updated: Translations - Italian (thanks cianecollazzo / azzo94) (#723)
- Updated: Translations - Korean (thanks neinnew) (#723, #742)
- Updated: Translations - Polish (thanks krzychu124) (#723)
- Updated: Translations - Portuguese (thanks BlackScout / BS_BlackScout) (#723)
- Updated: Translations - Russian (thanks Dmytro Lytovchenko / kvakvs) (#723)
- Updated: Translations - Spanish (thanks Nithox, obv) (#723, #742)
- Updated: Translations - Turkish (thanks Tayfun [Typhoon] / Koopr) (#723)
- Updated: Translations - Ukrainian (thanks Dmytro Lytovchenko / kvakvs) (#723)
- Meta: Thanks to CSUR Reloaded team for collaboration with #684! (#649, #503)
- Meta: Basic mod integration guide started (#696)
- Meta: Build guide updated to include note on Windows 10 ASLR, and reference hint paths (#693)
- Meta: Created GitHub org (`CitiesSkylinesMods`) and moved repo to it (`TMPE`) (#673)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)
- GitHub: [CitiesSkylinesMods/TMPE](https://github.com/CitiesSkylinesMods/TMPE)

See [Full Changelog](https://github.com/CitiesSkylinesMods/TMPE/blob/master/CHANGELOG.md) for details of all releases.

## Support Policy

Our team is happy to support you if:
- You are using the latest version of **TM:PE v11 STABLE** or **TM:PE v11 LABS**
- You are using a properly purchased and latest version of Cities: Skylines
- You provide useful information when [reporting a bug](https://github.com/CitiesSkylinesMods/TMPE/wiki/Report-a-Bug)

We will _not_ provide support if:
- You are using a pirated or old version of Cities: Skylines
- You are using an older version of the mod

TM:PE is only tested on and updated for the latest version of Cities: Skylines.

## Contributing

We welcome contributions from the community! See: [Contributing Guide](https://github.com/CitiesSkylinesMods/TMPE/wiki/Contributing)

## License

[MIT License](https://github.com/CitiesSkylinesMods/TMPE/blob/master/LICENSE) (free, open source)
