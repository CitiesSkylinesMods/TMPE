<p align="center"><img src="https://user-images.githubusercontent.com/16494272/59316295-ee189d00-8c7a-11e9-93a2-266292b6f3e9.png" width="740" height="232" /></p>
<p align="center">A mod for <strong>Cities: Skylines</strong> that gives you more control over road and rail traffic in your city<br /><br /></p>
<p align="center"><a href="https://steamcommunity.com/sharedfiles/filedetails/?id=583429740">Steam Workshop</a> • <a href="https://discord.gg/faKUnST">Discord Guild</a> • <a href="https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Installation">Installation Guide</a> • <a href="http://www.viathinksoft.de/tmpe/wiki">User Guide</a> • <a href="https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/issues">Issue Tracker</a> • <a href="https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Report-a-Bug">Report a Bug</a><br /></p>
<p align="center"><a href="https://steamcommunity.com/sharedfiles/filedetails/?id=583429740"><img src="https://img.shields.io/endpoint.svg?url=https://shieldsio-steam-workshop.jross.me/583429740" /></a> <a href="https://store.steampowered.com/app/255710/Cities_Skylines/"><img src="https://img.shields.io/badge/Game%20Version-1.12.1--f1-brightgreen.svg"></a> <a href="https://discord.gg/faKUnST"><img src="https://img.shields.io/discord/545065285862948894.svg?logo=discord&logoColor=F5F5F5" /></a> <a href="https://ci.appveyor.com/project/krzychu124/cities-skylines-traffic-manager-president-edition/branch/master"><img src="https://ci.appveyor.com/api/projects/status/dehkvuxk8b3h66e7/branch/master?svg=true" /></a></p>

## Current release

> Having problems with traffic despawning after updating roads or rails? Try [Broken Node Detector](https://steamcommunity.com/sharedfiles/filedetails/?id=1777173984) which helps detect a game bug. Collossal Order are aware of the issue.

#### Version [10.21](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/compare/10.20...10.21), 02/07/2019
- Added: Cims have individual driving styles to determine lane changes and driving speed (#263 #334)
- Added: Miles Per Hour option for speed limits (thanks kvakvs) (#384)
- Added: Selectable style (US, UK, EU) of speed sign in speed limits UI (thanks kvakvs) (#384)
- Added: Differentiate LABS, STABLE and DEBUG branches in UI (#326, #333)
- Added: Keybinds tab in mod options - choose your own shortcuts! (thanks kvakvs) (#382)
- Added: Show keyboard shortcuts in button tooltips where applicable (thanks kvakvs) (#382)
- Added: Basic support of offline mode for users playing on EA's Origin service (#333, #400)
- Improved: Avoid setting loss due to duplicate TM:PE subscriptions (#333, #306, #149, #190, #211, #400)
- Fixed: Vehicle limit count; compatibility with More Vehicles mod (thanks Dymanoid) (#362)
- Fixed: Mail trucks ignoring lane arrows (thanks Subaru & eudyptula for feedback) (#307, #338)
- Fixed: Vehicles stop in road trying to find parking (thanks eudyptula for investigating) (#259, #359)
- Fixed: Random parking broken (thanks s2500111 for beta testing) (#259, #359)
- Fixed: Pedestrian crossing restriction affects road-side parking (#259, #359)
- Fixed: 'Vanilla Trees Remover' is now compatible (thanks TPB for fixing) (#331, #332)
- Fixed: Single-lane bunching on DLS higher than 50% (#263 #334)
- Fixed: Lane changes at toll booths (also notified CO of bug in vanilla) (#225, #355)
- Fixed: Minor issues regarding saving/loading junction restrictions (#358)
- Fixed: Changes of default junction restrictions not reflected in UI overlay (#358)
- Fixed: Resetting stuck cims unpauses the simulation (#358, #351)
- Fixed: Treat duplicate TM:PE subscriptions as mod conflicts (#333, #306, #149, #190, #400)
- Fixed: TargetInvocationException in mod compatibility checker (#386, #333)
- Fixed: Issue with Paradox login blurring compatibility checker dialog (#404)
- Updated: Game version 1.12.1-f1 compatible (#403)
- Updated: Chinese translation (thanks Emphasia) (#375, #336)
- Updated: German translation (thanks kvakvs) (#384)
- Updated: Polish translation (thanks krzychu124) (#384, #333)
- Updated: Russian translation (thanks vitalii201 & kvakvs) (#327, #328)
- Updated: Renamed 'Realistic driving speeds' to 'Individual driving styles' (#334)
- Removed: Obsolete `TMPE.GlobalConfigGenerator` module (#367, #374)
- Meta: Separate binaries for Stable and Labs on GitHub release pages (#360)
- Meta: Initial documentation for release process in wiki (see `Contributing` page) (#360)
- Meta: Added GitHub issue templates for bugs, features, translations. (#272)
- Meta: Added `.editorconfig` file for IDE code indenting standardisation (#392, #384)
- Meta: Added entire `.vs/` and `.idea/` folders to `.gitignore` (#395, #382)
- Meta: Updated install guide to include section for EA Origin users (#333)
- Meta: Enable latest C# `LangVersion` in all projects (#398)

See [Full Changelog](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/blob/master/CHANGELOG.md) for details of earlier releases.

## Contributing

We welcome contributions from the community! See: [Contributing Guide](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Contributing)

## License

[MIT License](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/blob/master/LICENSE) (open source)

## Support Policy

The TM:PE team is happy to support you if you have any issues with the mod under the following conditions:
- You are using the latest version of the STABLE and/or LABS mod.
- You are using a properly purchased version of Cities: Skylines.

We will not provide support if:
- You are using a pirated version of Cities: Skylines.
- You are using an older version of the mod.
- You are using an older version of Cities: Skylines.

TM:PE is only tested on and updated for the latest version of Cities: Skylines.
