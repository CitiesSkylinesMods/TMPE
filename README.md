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
    <a href="https://store.steampowered.com/app/255710/Cities_Skylines/"><img src="https://img.shields.io/static/v1?label=cities:%20skylines&message=v1.13.0-f8&color=01ABF8&logo=unity" /></a>
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

Recent updates:

#### TM:PE V[11.4.0](https://github.com/CitiesSkylinesMods/TMPE/compare/11.3.2...11.4.0) STABLE, 22/05/2020

- Added: State machine for dedicated turning lanes (#755, #567)
- Fixed: Default turning lane on wrong side (#755, #671)
- Meta: Repeat application of turning lane shortcut will cycle through available options
- Steam: [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE V[11.5.0](https://github.com/CitiesSkylinesMods/TMPE/compare/11.4.0...11.5.0) LABS, 07/05/2020

- Added: Bulk customisation buttons on road info panel (#631, #691, #557, #542, #541, #539, #537)
- Added: While toolbar visible, click road = show info panel; right-click = hide (#631, #822, #557, #29)
- Added: Custom icons for road panel (thanks Chamëleon!) (#892, #887)
- Added: Hints for Lane Routing tools (thanks Klyte45 for color info) (#851, #587, #500, #410, #421)
- Added: Hints for Junction Restrictions, Priority Signs, Parking/Vehicle Restrictions (#868, #720)
- Added: Czech language translation (thanks jakubpatek, LordOrodreth) (#858)
- Fixed: Random UI bug when `UIView.GetAView()` is `null` (#868)
- Fixed: Toolbar can sometimes go off-screen or disappear (#877, #868, #849, #848, #819)
- Fixed: Toolbar buttons can escape toolbar (thanks to everyone who reported) (#850, #819)
- Fixed: Toolbar position limited to partial screen area (#819)
- Fixed: Parking button on toolbar always looks disabled (#858)
- Fixed: Timed Traffic Light tool doesn't reset state between uses (#880, #861, #893)
- Fixed: Confusing icon positions for junction restriction overlay (#845, #633)
- Fixed: Lane connector node highlights not working (thanks Xyrhenix for reporting!) (#851, #830)
- Fixed: Lane arrow 'reset' feature sometimes doesn't work (#891, #856, #738)
- Fixed: Mod options sliders don't update tooltip when dragged (#857, #849)
- Fixed: Slider tooltips update from wrong thread causing CTD (#880, #879)
- Fixed: `NetSegment.CalculateCorner()` exception for unsubbed roads (#883, #881)
- Updated: Toolbar rewrite - scalable, auto-arrange buttons, etc (#819, #523, #437, #38)
- Updated: Replace vanilla priority road checkbox with TMPE tools (#631, #542, #7)
- Updated: Consistent shortcuts for tools (#437, #587)
- Updated: Reduced call stack in game bridge, culled unused code (#852)
- Updated: Chinese Simplified translations (TianQiBuTian) (#720, #819, #851, #858)
- Updated: Chinese Traditional translations (jrthsr700tmax) (#720, #819, #851, #858)
- Updated: Czech translations (jakubpatek, LordOrodreth) (#720, #819, #851, #858)
- Updated: Dutch translations (Headspike, CaptainKlums) (#720, #819, #851, #858)
- Updated: English translations (kvakvs, kian.zarrin) (#720, #819, #851, #858)
- Updated: French translations (mjm92150) (#720, #819, #851, #858)
- Updated: German translations (BanditBloodwyn, Chamëleon, eilmannhenrik) (#720, #819, #851, #858)
- Updated: Hungarian translations (StummeH) (#720, #819, #851, #858)
- Updated: Japanese translations (mashitaro) (#720, #819, #851, #858)
- Updated: Polish translations (krzychu1245, Chamëleon) (#720, #819, #851, #858)
- Updated: Russian translations (kvakvs) (#720, #819, #851, #858)
- Updated: Turkish translations (revolter00) (#720, #819, #851, #858)
- Updated: Ukrainian translations (kvakvs) (#720, #819, #851, #858)
- Removed: `LogicUtil.CheckFlags()` and associated files evicted (#852)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

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
