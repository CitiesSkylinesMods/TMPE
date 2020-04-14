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

#### TM:PE V[11.3.2](https://github.com/CitiesSkylinesMods/TMPE/compare/11.3.1...11.3.2) LABS, 14/04/2020

- Fixed: Icons not showing when selecting node (thanks xenoxaos for reporting!) (#839, #838)
- Fixed: Bug in `StartPathFind()` if building missing (thanks ninjanoobslayer for reporting!) (#834, #840)
- Updated: `StartPathFind()` will automatically run diagnostic logging on errors (#834)
- Updated: Resident/Tourist status logic simplified (#837)

#### TM:PE V[11.3.1](https://github.com/CitiesSkylinesMods/TMPE/compare/11.3.0...11.3.1) LABS, 11/04/2020

- Fixed: Timed Traffic Lights bugs caused by v11.3.0 update (thanks to everyone who reported the bug!) (#828, #824)
- Updated: Trees Respiration mod is now compatible with TM:PE v11! (thanks Klyte45!) (#831, #614, #611, #563, #484)

#### TM:PE V[11.3.0](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.3...11.3.0) LABS, 10/04/2020

- Added: Advanced auto lane connector tool (select a node, then `Ctrl + S`) (#706, #703)
- Fixed: Stay-in-lane should not connect solitary lanes (#706, #617)
- Fixed: Lane arrows UI too small on some resolutions except at junctions (#726, #571)
- Updated: Lane connectors: `Shift + S` changed to `Ctrl + S` (see Options > Keybinds tab) (#706)
- Updated: Lane Arrows UI now respects UI scale slider (see Options > General tab) (#726)
- Updated: Improved UI for lane arrows tool (#726, #571)
- Updated: Translations (will add more info later) (#726)

#### TM:PE V[11.2.3](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.2...11.2.3) STABLE, 08/04/2020

- Fixed: Unable to set default speed limits for roads that need DLCs (#821, #818)

#### TM:PE V[11.2.3](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.2...11.2.3) LABS, 08/04/2020

- Fixed: Unable to set default speed limits for roads that need DLCs (#821, #818)

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
