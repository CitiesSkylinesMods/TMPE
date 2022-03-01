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
    <a href="https://store.steampowered.com/app/255710/Cities_Skylines/"><img src="https://img.shields.io/static/v1?label=cities:%20skylines&message=v1.14.0-f9&color=01ABF8&logo=unity" /></a>
    <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252"><img src="https://img.shields.io/github/v/release/CitiesSkylinesMods/TMPE?label=stable&color=7cc17b&logo=steam&logoColor=F5F5F5" /></a>
    <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=2489276785"><img src="https://img.shields.io/github/v/release/CitiesSkylinesMods/TMPE?include_prereleases&label=test&color=f7b73c&logo=steam&logoColor=F5F5F5" /></a>
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

> See [Full Changelog](https://github.com/CitiesSkylinesMods/TMPE/blob/master/CHANGELOG.md) for details of all releases.

- [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252) (fully tested releases)
- [TM:PE v11 TEST](https://steamcommunity.com/sharedfiles/filedetails/?id=2489276785) (latest beta test releases)
- [Download Binaries](https://github.com/CitiesSkylinesMods/TMPE/releases) (for non-Steam users)
- [Installation Guide](https://github.com/CitiesSkylinesMods/TMPE/wiki/Installation) (for all users)

### Recent releases:

> Date format: dd/mm/yyyy

#### TM:PE V11.6.5.0 TEST, 16/02/2022

- [Meta] Due to targeted malware, we are unable to provide support if you have mods by `Chaos`/`Holy Water`/`drok`, even if those mods are currently malware-free #1391 (TM:PE Team)
- [Meta] Compatible with Cities: Skylines v1.14.0-f9 #1387 (krzychu124)
- [New] Overlays mod option to show default speed when overriding segment/lane speeds #1404 (aubergine18)
- [New] Added API for external mods to query TM:PE mod options #1378 #1376 (aubergine18)
- [Mod] Compatible: `Reversible Tram AI` full compatibility #1386 #1353 (sway2020)
- [Mod] Compatible: `Supply Chain Coloring` workshop version only #1390 (aubergine18)
- [Mod] Compatible: `Transfer Broker BETA` workshop version only #1390 (aubergine18)
- [Mod] Incompatible: `TM:PE LABS` - discontinued (replaced by TM:PE TEST) #1390 (aubergine18)
- [Mod] Incompatible: `Traffic Manager (Curated)` - unsupported clone #1390 (aubergine18)
- [Mod] Incompatible: `TMPE:TrafficManager全部汉化` - unsupported clone #1390 (aubergine18)
- [Fixed] `StackOverflowException` due to `What's New` panel autolayout #1393 #1314 (krzychu124)
- [Fixed] Skip searching for best emergency lane for non-car vehicles #1408 (krzychu124)
- [Updated] Speed Limits: Always use themed icons in segment/lane modes #1404 (aubergine18)
- [Updated] Extend Harmony patch manager to allow manual patches #1386 #1361 (sway2020)
- [Updated] Various code clean-up and micro optimisations #1413 #1407 #1406 #1401 #1400 #1399 #1398 #1397 #1396 (egi)
- [Updated] Remove duplicate button clean-up code in lifecycle #1375 (aubergine18)
- [Updated] Internal restructuring of mod options code #1403 #1369 #1370 #1371 #1373 #1374 (aubergine18)
- [Updated] Translations for mod options, speed limits, traffic lights #1415 (krzychu124, freddy0419, Natchanok Kulphiwet, MamylaPuce, ipd, 田七不甜 TianQiBuTian, TwotoolusFLY_LSh.st, Never2333, 문주원 sky162178, MenschLennart, Chamëleon, John Deehe, Skazov, AlexofCA, CrankyAnt, Иван Соколов)
- [Updated] Update assembly info metadata #1417 (krzychu124)
- [Steam] [TM:PE v11 TEST](https://steamcommunity.com/sharedfiles/filedetails/?id=2489276785)

#### TM:PE V11.6.4.8 STABLE, 10/02/2022

- [Meta] TM:PE 11.6.4-hotfix-8
- [Meta] Bugfix for vehicle spawning/delivery on restricted lanes
- [Mod] Malware: We are treating all mods by Chaos/Holy Water (same person) as targeted malware #1389 #1388 (aubergine18)
- [Fixed] Allow vehicles to use restricted lanes when spawning/delivering #1381 #1380 #494 #85 (krzychu124)
- [Steam] [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE V11.6.4.7 STABLE, 06/02/2022

- [Meta] TM:PE 11.6.4-hotfix-7
- [Meta] Bugfix for default speeds which affects speed limits tool, overlays, and roundabout curvature speed
- [Fixed] Default netinfo speed should only inspect customisable lanes #1362 #1346 (aubergine18)
- [Fixed] Fix `SPEED_TO_MPH` value in `ApiConstants.cs` #1364 #1363 #988 (aubergine18)
- [Removed] Obsolete: `SPEED_TO_MPH` and `SPEED_TO_KMPH` in `Constants.cs` #1367 #1364 #1363 (aubergine18)
- [Steam] [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

## Support Policy

Our team is happy to support you if:
- You are using the latest version of **TM:PE v11 STABLE** or **TM:PE v11 TEST**
- You are using a properly purchased and latest version of Cities: Skylines
- You provide useful information when [reporting a bug](https://github.com/CitiesSkylinesMods/TMPE/wiki/Report-a-Bug)

We will _not_ provide support if:
- You are using a pirated or old version of Cities: Skylines
- You are using an older version of the mod
- You are using any mod by Holy Water ([due to malware targeted at our team](https://steamcommunity.com/workshop/filedetails/discussion/1637663252/4731597528356140067/))

TM:PE is only tested on and updated for the latest version of Cities: Skylines.

## Contributing

We welcome contributions from the community! See: [Contributing Guide](https://github.com/CitiesSkylinesMods/TMPE/wiki/Contributing)

## License

[MIT License](https://github.com/CitiesSkylinesMods/TMPE/blob/master/LICENSE) (free, open source)
