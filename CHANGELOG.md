# Cities: Skylines - Traffic Manager: _President Edition_

# Changelog

<a href="https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252"><img src="https://img.shields.io/github/v/release/CitiesSkylinesMods/TMPE?label=stable&color=7cc17b&logo=steam&logoColor=F5F5F5" /></a> <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141"><img src="https://img.shields.io/github/v/release/CitiesSkylinesMods/TMPE?include_prereleases&label=labs&color=f7b73c&logo=steam&logoColor=F5F5F5" /></a> <a href="https://github.com/CitiesSkylinesMods/TMPE/releases"><img src="https://img.shields.io/github/v/release/CitiesSkylinesMods/TMPE?label=downloads&include_prereleases&logo=ipfs&logoColor=F5F5F5" /></a> <a href="https://github.com/CitiesSkylinesMods/TMPE/wiki/Installation"><img src="https://img.shields.io/badge/install%20guide-wiki-blue?logo=koding&logoColor=F5F5F5" /></a> <a href="https://discord.gg/faKUnST"><img src="https://img.shields.io/discord/545065285862948894?color=7289DA&label=chat&logo=discord" /></a>

This changelog includes all versions and major variants of the mod going all the way back to March 2015, just 4 days after Cities: Skylines was released!

> **Legend:**
>  
> * **C:SL** = Cities: Skylines game updates (for reference)
> * **TM:PE** = Traffic Manager: President Edition
>     * TM:PE v11 STABLE - TM:PE versions 11.0 and above (stable releases)
>     * TM:PE v11 LABS - TM:PE versions 11.0 and above (test releases)
>     * TM:PE v11 ALPHA - TM:PE versions 11.0-alpha1 to 11.0-alpha12 (later renamed to TM:PE v11 LABS)
>     * TM:PE LABS - TM:PE versions 10.14 to 10.21.1 (later renamed to TM:PE v11 STABLE)
>     * TM:PE STABLE - TM:PE versions 10.14 to 10.20
> * **TPP2** = Traffic++ V2
> * **TM:IAI** = Traffic Manager + Improved AI
> * **TMPlus** = Traffic Manager Plus
> * **TPP:AI** - Traffic++ Improved AI
> * **TPP** = Traffic++
> * **TM** = Traffic Manager
> * **CSLT** = Cities Skylines Traffic (later renamed to Traffic++)
> * **TLM** = Traffic Lights Manager (later renamed to Traffic Manager)
>  
> Date format: dd/mm/yyyy

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

#### TM:PE V[11.3.2](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.3...11.3.2) STABLE, 16/04/2020

- Added: Advanced auto lane connector tool (select a node, then `Ctrl + S`) (#706, #703)
- Fixed: Icons not showing when selecting node (thanks xenoxaos for reporting!) (#839, #838)
- Fixed: Bug in `StartPathFind()` if building missing (thanks ninjanoobslayer for reporting!) (#834, #840)
- Fixed: Timed Traffic Lights bugs caused by v11.3.0 update (thanks to everyone who reported the bug!) (#828, #824)
- Fixed: Stay-in-lane should not connect solitary lanes (#706, #617)
- Fixed: Lane arrows UI too small on some resolutions except at junctions (#726, #571)
- Updated: `StartPathFind()` will automatically run diagnostic logging on errors (#834)
- Updated: Resident/Tourist status logic simplified (#837)
- Updated: Trees Respiration mod is now compatible with TM:PE v11! (thanks Klyte45!) (#831, #614, #611, #563, #484)
- Updated: Lane connectors: `Shift + S` changed to `Ctrl + S` (see Options > Keybinds tab) (#706)
- Updated: Lane Arrows UI now respects UI scale slider (see Options > General tab) (#726)
- Updated: Improved UI for lane arrows tool (#726, #571)
- Updated: British translations (charlco, kvakvs, kian.zarrin) (#726)
- Updated: Chinese Simplified translations (wxf26054) (#726)
- Updated: Dutch translations (CaptainKlums) (#726)
- Updated: English translations (kian.zarrin, kvakvs) (#726)
- Updated: French translations (mjm92150, Stuart) (#726)
- Updated: German translations (eilmannhenrik, BanditBloodwyn, Rodirik, Cirez) (#726)
- Updated: Hungarian translations (szekely.david9) (#726)
- Updated: Italian translations (emiliomacchione, DelvecchioSimone) (#726)
- Updated: Korean translations (TwotoolusFLY_LSh.s, neinnew) (#726)
- Updated: Polish translations (krzychu1245, dom.kawula)
- Updated: Portuguese translations (BS_BlackScout) (#726)
- Updated: Russian translations (kvakvs) (#726)
- Updated: Turkish translations (revolter00, Koopr) (#726)
- Updated: Ukranian translations (kvakvs) (#726)
- Meta: Cumulative updates from 11.3.0, 11.3.1 and 11.3.2
- Steam: [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE V[11.4.0](https://github.com/CitiesSkylinesMods/TMPE/compare/11.3.2...11.4.0) LABS, 14/04/2020

- Added: State machine for dedicated turning lanes (#755, #567)
- Fixed: Default turning lane on wrong side (#755, #671)
- Meta: Repeat application of turning lane shortcut will cycle through available options
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V[11.3.2](https://github.com/CitiesSkylinesMods/TMPE/compare/11.3.1...11.3.2) LABS, 14/04/2020

- Fixed: Icons not showing when selecting node (thanks xenoxaos for reporting!) (#839, #838)
- Fixed: Bug in `StartPathFind()` if building missing (thanks ninjanoobslayer for reporting!) (#834, #840)
- Updated: `StartPathFind()` will automatically run diagnostic logging on errors (#834)
- Updated: Resident/Tourist status logic simplified (#837)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V[11.3.1](https://github.com/CitiesSkylinesMods/TMPE/compare/11.3.0...11.3.1) LABS, 11/04/2020

- Fixed: Timed Traffic Lights bugs caused by v11.3.0 update (thanks to everyone who reported the bug!) (#828, #824)
- Updated: Trees Respiration mod is now compatible with TM:PE v11! (thanks Klyte45!) (#831, #614, #611, #563, #484)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V[11.3.0](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.3...11.3.0) LABS, 10/04/2020

- Added: Advanced auto lane connector tool (select a node, then `Ctrl + S`) (#706, #703)
- Fixed: Stay-in-lane should not connect solitary lanes (#706, #617)
- Fixed: Lane arrows UI too small on some resolutions except at junctions (#726, #571)
- Updated: Lane connectors: `Shift + S` changed to `Ctrl + S` (see Options > Keybinds tab) (#706)
- Updated: Lane Arrows UI now respects UI scale slider (see Options > General tab) (#726)
- Updated: Improved UI for lane arrows tool (#726, #571)
- Updated: British translations (charlco, kvakvs, kian.zarrin) (#726)
- Updated: Chinese Simplified translations (wxf26054) (#726)
- Updated: Dutch translations (CaptainKlums) (#726)
- Updated: English translations (kian.zarrin, kvakvs) (#726)
- Updated: French translations (mjm92150, Stuart) (#726)
- Updated: German translations (eilmannhenrik, BanditBloodwyn, Rodirik, Cirez) (#726)
- Updated: Hungarian translations (szekely.david9) (#726)
- Updated: Italian translations (emiliomacchione, DelvecchioSimone) (#726)
- Updated: Korean translations (TwotoolusFLY_LSh.s, neinnew) (#726)
- Updated: Polish translations (krzychu1245, dom.kawula)
- Updated: Portuguese translations (BS_BlackScout) (#726)
- Updated: Russian translations (kvakvs) (#726)
- Updated: Turkish translations (revolter00, Koopr) (#726)
- Updated: Ukranian translations (kvakvs) (#726)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V[11.2.3](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.2...11.2.3) STABLE, 08/04/2020

- Fixed: Unable to set default speed limits for roads that need DLCs (#821, #818)
- Steam: [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE V[11.2.3](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.2...11.2.3) LABS, 08/04/2020

- Fixed: Unable to set default speed limits for roads that need DLCs (#821, #818)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V[11.2.2](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.1...11.2.2) STABLE, 26/03/2020

- Fixed: GetModName() when user has two mods with same assembly name/version (#812, #811)
- Updated: Game version badges in readme (#806)
- Updated: Mod version and changelogs (#816)
- Steam: [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE V[11.2.2](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.1...11.2.2) LABS, 26/03/2020

- Fixed: GetModName() when user has two mods with same assembly name/version (#812, #811)
- Updated: Game version badges in readme (#806)
- Updated: Mod version and changelogs (#816)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V[11.2.1](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.0...11.2.1) STABLE, 26/03/2020

- Fixed: CustomPathManager nullpointer on exit from asset/map editor (#794)
- Fixed: Add missing Trolleybus vehicle category (#794)
- Fixed: CustomPathManager NullPointerException on second load (#794)
- Steam: [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE V[11.2.1](https://github.com/CitiesSkylinesMods/TMPE/compare/11.2.0...11.2.1) LABS, 29/03/2020

- Fixed: CustomPathManager nullpointer on exit from asset/map editor (#794)
- Fixed: Add missing Trolleybus vehicle category (#794)
- Fixed: CustomPathManager NullPointerException on second load (#794)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V[11.2.0](https://github.com/CitiesSkylinesMods/TMPE/compare/11.1.2...11.2.0) LABS, 26/03/2020

- Added: Trolleybus AI (#794)
- Fixed: `PathUnits.m_vehicleTypes` error after Sunset Harbor game update (#794)
- Fixed: Priority signs for trolleybuses (#794)
- Fixed: Build process deployed TrafficManager.dll twice ( #776, #775)
- Improved: Performance of hot-reloads of dev builds (#764, #730)
- Improved: Consolidate error prompts in to helper class (#774)
- Updated: Add missing entries and fix typos in changelog (#777, #779)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V[11.2.0](https://github.com/CitiesSkylinesMods/TMPE/compare/11.1.0...11.2.0) STABLE, 26/03/2020

- Added: Trolleybus AI (#794)
- Fixed: `PathUnits.m_vehicleTypes` error after Sunset Harbor game update (#794)
- Fixed: Priority signs for trolleybuses (#794)
- Fixed: Build process deployed TrafficManager.dll twice ( #776, #775)
- Improved: Performance of hot-reloads of dev builds (#764, #730)
- Improved: Consolidate error prompts in to helper class (#774)
- Updated: Add missing entries and fix typos in changelog (#777, #779)
- Steam: [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)
- GitHub: [CitiesSkylinesMods/TMPE](https://github.com/CitiesSkylinesMods/TMPE)

#### TM:PE V[11.1.2](https://github.com/CitiesSkylinesMods/TMPE/compare/11.1.1-hotfix1...11.1.2) LABS, 02/03/2020

- Fixed: One-click traffic lights wrong way on RHT maps, murdering pedestrians (#770, #769, #690)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V[11.1.1-hotfix1](https://github.com/CitiesSkylinesMods/TMPE/compare/11.1.1...11.1.1-hotfix1) LABS, 01/03/2020

- Fixed: Vehicles stopping at Yield signs (#761, #756)
- Fixed: Missing despawn buttons on cim and vehicle info panels (#765, #763, #759)
- Fixed: Info panel not closing after despawning a cim or tourist (#765)
- Fixed: Faulty UI on tourist despawn button (#765)
- Updated: Docs - Reference paths for EA Origin deployed game (thanks DannyDannyDan) ( #751)
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

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

#### TM:PE V[11.1.0](https://github.com/CitiesSkylinesMods/TMPE/compare/11.0...11.1.0) LABS, 11/02/2020

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
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V[11.0](https://github.com/CitiesSkylinesMods/TMPE/compare/10.21.1...11.0) STABLE, 03/02/2020

- Contains ~100 improvements from TM:PE v11 ALPHA previews, including:
    - Timed traffic lights: Add default sequence (Ctrl+Click a junction)
    - Lane arrows: Turning lanes (Ctrl+Click a junction, or Alt+Click a segment)
    - Vanilla traffic lights: Remove or Disable auto-placed traffic lights (buttons in mod options)
    - New [languages](https://crowdin.com/project/tmpe): Hungarian, Turkish, Ukrainian; all other languages updated!
    - Migration to Harmony for improved compatibility- Improved: Better segment hovering when mouse near segment (thanks kianzarrin!) (#624, #576)
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
- Meta: Renamed LABS and ALPHA workshop pages to V11 STABLE and V11 LABS respectively
- Steam: [TM:PE v11 STABLE](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE V[11.0](https://github.com/CitiesSkylinesMods/TMPE/compare/10.21.1...11.0) LABS, 03/02/2020

- Contains ~100 improvements from TM:PE v11 ALPHA previews, including:
    - Timed traffic lights: Add default sequence (Ctrl+Click a junction)
    - Lane arrows: Turning lanes (Ctrl+Click a junction, or Alt+Click a segment)
    - Vanilla traffic lights: Remove or Disable auto-placed traffic lights (buttons in mod options)
    - New [languages](https://crowdin.com/project/tmpe): Hungarian, Turkish, Ukrainian; all other languages updated!
    - Migration to Harmony for improved compatibility- Improved: Better segment hovering when mouse near segment (thanks kianzarrin!) (#624, #576)
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
- Meta: Renamed LABS and ALPHA workshop pages to V11 STABLE and V11 LABS respectively
- Steam: [TM:PE v11 LABS](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### C:SL 1.12.3-f2 ("Paradox Launcher"), 22/01/2020

- Paradox Launcher app added
- Pain.

#### TM:PE V11 ALPHA 11.0-alpha12, 12/01/2020

- Fixed: Array index error when Lane Arrow tool selected (#606, #607)
- Fixed: Removing junction from traffic light group not working (thanks leaderofthemonkeys for finding this!) (#605)
- Fixed: Detection of compatible Timed Traffic Lights node for copying Traffic Light setup (#605)
- Fixed: Cursor flickering when tool is selected (#607)
- Updated: Added two obsolete versions of TM:PE to incompatible mod checker (#610)
- Updated: Language - Italian (Simone Delvecchio / DelvecchioSimone) (#603)
- Updated: Language - Korean (neinnew) (#603)
- Updated: Language - Japanese (mashitaro) (#603)
- Updated: Language - Turkish (Tayfun Bilgi / Tayfun [Typhoon]), Rıdvan SAYLAR / ridvan.saylar) (#603)
- Updated: Language - Portuguese (BlackScout / BS_BlackScout) (#603)
- Updated: Language - Chinese Traditional (@jrthsr700tmax) (#603)
- Updated: Language - Ukrainian (Dmytro Lytovchenko / kvakvs) (#603)
- Updated: Language - Russian (Dmytro Lytovchenko / kvakvs) (#603)
- Updated: Language - French (Guillaume Turchini / orion78fr) (#603)
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V11 ALPHA 11.0-alpha11, 25/12/2019

- Added: One-click timed traffic light set-up (thanks Kian Zarrin!) (#554, #540, #5, #324, #572)
- Added: Turkish language (by Tayfun Bilgi for his dad!) (#572)
- Added: Ukrainian language (thanks kvakvs) (#572)
- Improved: Half-overlay indicates which side of segment will get turning lane (#564, #548)
- Improved: Better node selection circles + code cleanup (#564, #555)
- Improved: Mod options tabs can now scroll to fit more content (#553, #552)
- Improved: Disambiguate naming convention for left hand traffic (#580, #577, #581)
- Improved: More robust CSV parsing for translations (#589, #574)
- Fixed: Ensure valid language used if selected language no longer exists (#579)
- Updated: Turkish translations (thanks Tayfun Bilgi / Tayfun [Typhoon]) (#591, #599)
- Updated: French translations (thanks Guillaume Turchini / orion78fr) (#591)
- Updated: Japanese translations (thanks しょしょ02 / yamadatarounohosi) (#591)
- Updated: Chinese Simplified translations (thanks 田七不甜 / TianQiBuTian) (#591, #599)
- Updated: Chinese Traditional translations (thanks jrthsr700tmax) (#599, #595)
- Updated: Ukrainian translations (thanks Dmytro Lytovchenko / kvakvs) (#591, #599)
- Updated: Russian translations (thanks Dmytro Lytovchenko / kvakvs) (#591, #599)
- Updated: Polish translations (thanks krzychu124) (#591)
- Updated: Portuguese translations (thanks BS_BlackScout) (#599)
- Updated: English translations (thanks kian.zarrin & aubergine18) (#591)
- Meta: Updated `StyleCop.Analyzers` to latest version for compatibility with latest Nuget (#591)
- Meta: Added readme file with link to localisation guide in the translations folder (#596)
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V11 ALPHA 11.0-alpha10, 23/11/2019

- Added: Lane arrow tool - shortcuts to create separate turning lanes (thanks kianzarrin!) (#538, #537)
- Fixed: Null reference error in `TrafficManager.UI.TrafficManagerTool.OnEnable` (#570)
- Fixed: Bug in `IterateNodeSegments` + code clean-up (thanks kianzarrin) (#549, #550)
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V11 ALPHA 11.0-alpha9, 10/11/2019

- Added: Features to disable auto-traffic lights, and delete all traffic lights (thanks Craxy & Sqoops) (#320, #390, #535)
- Fixed: `IndexOutOfRange` error in manual traffic lights tool (thanks leonpeonleon) (#545)
- Fixed: Path find stats fixed & faster, benchmark profile fixed (#536)
- Fixed: Typos and missing key in translations (thanks TianQiBuTian) (#529, #528)
- Fixed: Translations not working when using game translation mods (thanks TianQiBuTian) (#533, #534)
- Updated: Translations - Chinese Simplified - 田七不甜 (thanks TianQiBuTian) (#536, #530)
- Updated: Translations - Chinese Traditional - 許景翔 (thanks gk50125012) (#536)
- Updated: Translations - Portuguese - Alan Willian Duarte (thanks nipodemos13) (#536)
- Updated: Translations - Japanese - thanks mashitaro (#536)
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### C:SL 1.12.2-f3 ("Modern City Center" and "Downtown Radio"), 07/11/2019

* Added: [Modern City Center](https://skylines.paradoxwikis.com/Modern_City_Center) DLC
* Added: [Downtown Radio](https://skylines.paradoxwikis.com/Downtown_Radio) DLC

#### TM:PE V11 ALPHA 11.0-alpha8, 04/10/2019

- Added: Junctions now show traffic light status when using toggle traffic light tool (#527)
- Updated: Add outline to lane connector lines and improve arcs (#526, #523)
- Updated: Improve speed limits overlay performance while camera still (#521, #520)
- Updated: New translation/localisation system (#509, #493)
- Fixed: Minor typos in new translation/localisation system (thanks TianQiBuTian!) (#528)
- Fixed: Remove decorative networks from speed limits manager (#513, #510, #378)
- Meta: Deprecated issues closed (#336, #169)
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V11 ALPHA 11.0-alpha7, 04/09/2019

- Added: Hungarian translations (thanks JozsefHUNGepiM) (#491, #492)
- Fixed: Train restriction vehicle icons regression (#483)
- Fixed: Remove trace logging from release builds (thanks TLHeart60) (#454, #499)
- Fixed: Ignore decorative and malconfigured networks in Speed Limits Manager (#513)
- Updated: Compatible with Tree Respiration mod (#484)
- Updated: Compatible with Vehicle Wealthizer mod (#490, #488)
- Updated: More code clean-up (#350)
- Meta: Update GitHub issue creation templates (#486)
- Meta: Updated documentation on wiki and GitHub (#310, #79, #465, #474, #466)
- Meta: Pathfinds display temporarily disabled (testing FPS Booster mod)
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V11 ALPHA 11.0-alpha6, 04/08/2019

- Added: "Cargo Info" mod is incompatible (#478)
- Fixed: Vehicles pausing unexpectedly at junctions (#448, #473)
- Updated: Lots more code clean-up (#467, #475, #438, #435, #476)
- Meta: "Cargo Info" mod found to break outside connections and cause array index errors.
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V11 ALPHA 11.0-alpha5, 31/07/2019

- Updated: Lots of code clean-up (#461, #349, #377, #451)
- Meta: Build process will now error if `in` is used without a `readonly struct` (thanks dymanoid!) (#463)
- Meta: See `Contributing` guide in GitHub wiki if you get build errors due to #463
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V11 ALPHA 11.0-alpha4, 26/07/2019

- Fixed: Adding parking restriction doesn't move already parked cars (#445, #459)
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V11 ALPHA 11.0-alpha3, 25/07/2019

- Added: Mod checker lists mods in `TMPE.log` (#443)
- Improved: Show version in mod checker title bar (#458)
- Fixed: Mod checker crashes if blank line in `incompatible_mods.txt` resource (#441)
- Fixed: Trace log appearing in `RELEASE` builds (#454, #455)
- Updated: Mod checker will always scan for duplicate TM:PE, even if disabled (#434, #443, #433)
- Updated: French translations (thanks mjm92150) (#453)
- Meta: `TMPE.API` now has a `RELEASE LABS` build (#456)
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V11 ALPHA 11.0-alpha2, 23/07/2019

- Fixed: Unable to set "no limit" speed, and speeds over 140 km/h weren't showing as "no limit" (#449, #446)
- Meta: Lots more back-end code clean-up (#430, #436, #349)
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)

#### TM:PE V11 ALPHA 11.0-alpha1, 17/07/2019

- Updated: Migration to Harmony framework (thanks LinuxFan!) (#428, #427, #260, #119)
- Updated: Chinese translation updates (thanks Emphasia) (#417)
- Meta: Updated changelog to include more of TM:PE history (#422)
- Meta: LinuxFan workshop page (STABLE 10.20) no longer updated, so LABS page had to remain on v10.21.1 (bugfix of STABLE 10.20)
- Meta: New ALPHA page was created to allow test releases of v11 branch
- Steam: [TM:PE v11 ALPHA](https://steamcommunity.com/sharedfiles/filedetails/?id=1806963141)
- Maintainer: aubergine18 (GitHub user aubergine10)
- GitHub: [krzychu124/Cities-Skylines-Traffic-Manager-President-Edition](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition)

#### TM:PE LABS [10.21.1 hotfix](https://github.com/CitiesSkylinesMods/TMPE/compare/10.21...10.21.1), 06/07/2019

- Fixed: Speed panel tanks fps if train tracks on screen (thanks rlas & DaEgi01!) (#411, #413)
- Meta: Main changelog refactored (#412)
- Steam: [Traffic Manager: President Edition (LABS)](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE LABS [10.21](https://github.com/CitiesSkylinesMods/TMPE/compare/10.20...10.21), 02/07/2019

- Added: Cims have individual driving styles to determine lane changes and driving speed (#263 #334)
- Added: Miles Per Hour option for speed limits (thanks kvakvs) (#384)
- Added: Selectable style (US, UK, EU) of speed sign in speed limits UI (thanks kvakvs) (#384)
- Added: Differentiate LABS, STABLE and DEBUG branches in UI (#326, #333)
- Added: Keybinds tab in mod options - choose your own shortcuts! (thanks kvakvs) (#382)
- Added: Show keyboard shortcuts in button tooltips where applicable (thanks kvakvs) (#382)
- Added: Basic support of offline mode for users playing on EA's Origin service (#333, #400)
- Improved:: Avoid setting loss due to duplicate TM:PE subscriptions (#333, #306, #149, #190, #211, #400)
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
- Fixed: `TargetInvocationException` in mod compatibility checker (#386, #333)
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
- Steam: [Traffic Manager: President Edition (LABS)](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### C:SL 1.12.1-f2, 04/06/2019

- Fixed: Numerous bugs in Campus DLC

#### TM:PE STABLE 10.20, 21/05/2019

- Added: Japanese language (thanks mashitaro) (#258)
- Updated: Compatible with C:SL 1.12.0-f5
- Updated: Korean translation (thanks Twotoolus-FLY-LShst) (#294)
- Updated: French translation (thanks PierreTSE) (#311)
- Updated: Moved "Delete" step button on timed traffic lights (#283, #285)
- Updated: "Vanilla Trees Remover" as incompatible mod (it breaks mod options screen) (#271, #290)
- Updated: Mod incompatibility checker can now be disabled, or skip disabled mods (#264, #284, #286)
- Updated: Chinese language (thanks Emphasia) (#285, #286)
- Fixed: Mod options overlapping issue (#250, #266)
- Meta: This was the final release of the v10 STABLE branch
- Steam: [Traffic Manager: President Edition (STABLE)](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE LABS 10.20, 21/05/2019

- Updated: Compatible with C:SL 1.12.0-f5
- Updated: Korean translation (thanks Twotoolus-FLY-LShst) (#294)
- Updated: French translation (thanks PierreTSE) (#311)
- Steam: [Traffic Manager: President Edition (LABS)](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### C:SL 1.12.0-f5 (Campus), 21/05/2019

- Added: University areas & lots of buildings
- Added: Choose bus to use on bus lines

#### TM:PE LABS 10.19, 20/04/2019

- Added: Japanese language (thanks mashitaro) (#258)
- Updated: Moved "Delete" step button on timed traffic lights (#283, #285)
- Updated: "Vanilla Trees Remover" as incompatible mod (it breaks mod options screen) (#271, #290)
- Updated: Mod incompatibility checker can now be disabled, or skip disabled mods (#264, #284, #286)
- Updated: Chinese language (thanks Emphasia) (#285, #286)
- Fixed: Mod options overlapping issue (#250, #266)
- Steam: [Traffic Manager: President Edition (LABS)](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE LABS 10.18, 29/03/2019

- Fixed: Parking AI: Cars do not spawn at outside connections (#245)
- Fixed: Trams perform turns on red (#248)
- Updated: Service Radius Adjuster mod by Egi removed from incompatible mods list (#255)
- Steam: [Traffic Manager: President Edition (LABS)](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE STABLE 10.18, 28/03/2019

- Fixed: Parking AI: Cars do not spawn at outside connections (#245)
- Fixed: Trams perform turns on red (#248)
- Updated: Service Radius Adjuster mod by Egi removed from incompatible mods list (#255)
- Steam: [Traffic Manager: President Edition (STABLE)](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE STABLE 10.17, 24/03/2019

- Added: Synchronized code and version with labs version
- Updated: Russian translation (thanks vitalii201!) (#207)
- Updated: List of incompatible mods (#115)
- Removed: Stable version from list of conflicting mods (#168)
- Improved: Turn-on-red can now be toggled for unpreferred (far-side) turns between one-ways
- Improved: Train behaviour at shunts: Trains now prefer to stay on their track (#230)
- Improved: Parking AI: Improved public transport (PT) usage patterns, mixed car/PT paths are now possible (#218)
- Fixed: and optimized lane selection for U-turns and at dead ends (#101)
- Fixed: Parking AI: Tourist cars despawn because they assume they are at an outside connection (#218)
- Fixed: Parking AI: Return path calculation did not accept beautification segments (#218)
- Fixed: Parking AI: Cars/Citizens waiting for a path might jump around (#218)
- Fixed: Vanilla lane randomization does not work as intended at highway transitions (#112)
- Fixed: Vehicles change lanes at tollbooths (#225)
- Fixed: Path-finding: Array index is out of range due to a race condition (#221)
- Fixed: Citizen not found errors when using walking tours (#219)
- Fixed: Timed light indicator only visible when any timed light node is selected (#222)
- Meta: Introduced new versioning scheme (10.17 instead of 1.10.17)
- Meta: There was no 10.16 release for TM:PE STABLE
- Steam: [Traffic Manager: President Edition (STABLE)](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE LABS 10.17, 23/03/2019

- Added: Turn-on-red can now be toggled for unpreferred (far-side) turns between one-ways (#121)
- Improved: Train behaviour at shunts: Trains now prefer to stay on their track (#230)
- Improved: Parking AI - Improved public transport (PT) usage patterns, mixed car/PT paths are now possible  (#218)
- Fixed: Lane selection for U-turns and at dead ends (also optimised) (#101)
- Fixed: Parking AI - Tourist cars despawn because they assume they are at an outside connection (#218)
- Fixed: Parking AI - Return path calculation did not accept beautification segments (#218)
- Fixed: Parking AI - Cars/Citizens waiting for a path might jump around (#218)
- Fixed: Vanilla lane randomization does not work as intended at highway transitions (#112)
- Fixed: Vehicles change lanes at tollbooths (#225)
- Fixed: Path-finding: Array index is out of range due to a race condition (#221)
- Fixed: Citizen not found errors when using walking tours (#219)
- Fixed: Timed light indicator only visible when any timed light node is selected (#222)
- Updated: Compatible with C:SL 1.11.1-f4
- Updated: Synchronized code and version with stable version
- Updated: List of incompatible mods (#115)
- Updated: Russian translation (thanks to vitalii201 for translating) (#207)
- Removed: Stable version from list of incompatible mods (#168)
- Meta: Introduced new versioning scheme (10.17 instead of 1.10.17)
- Steam: [Traffic Manager: President Edition (LABS)](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE STABLE 1.10.15, 05/03/2019

- Improved: Compatibility with LABS version
- Updated: Russian translation (thanks vitalii201!)
- Steam: [Traffic Manager: President Edition (STABLE)](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE STABLE 1.10.14, 03/03/2019

- Added: Updates from LABS version (excluding experimental features and icons)
- Fixed: Path-finding: Array index is out of range due to a race condition (#227)
- Fixed: Citizen not found errors when using walking tours (#223)
- Fixed: Timed light indicator only visible when any timed light node is selected (#222)
- Fixed: Some option labels are too short (thanks krzychu124!) (#235)
- Meta: Original TM:PE workshop page gets "STABLE" suffix
- Steam: [Traffic Manager: President Edition (STABLE)](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.11.1-f4, 27/02/2019

- Fixed: Remove duplicate map

#### TM:PE LABS 1.10.16, 24/02/2019

- Improved: New icons for `empty` and `remove_priority_sign` settings (thanks aubergine10 for those icons) (#75, #77)
- Fixed: Problem with vehicle despawn after road upgrade/remove (thanks pcfantasy for implementation suggestion)(#86, #101)
- Fixed: problem with vehicles unable to choose lane when U-turn at dead-end (thanks pcfantasy for implementation and aubergine10 for testing)(#101)
- Fixed: problem when user couldn't change state of 'Turn on Red' while `enabled_by_default` option not selected (thanks Sp3ctre18 for bug confirmation) (#102)
- Fixed: Fixed 'silent error' inside log related with "Esc key handler" (#92)
- Updated: Greatly improved incompatible mod scanner, added dialog to list and unsubscribe incompatible mods (#91)
- Updated: Changed mod name in Content Manager to __TM:PE__
- Updated: Added missing logic for noise density calculations (thanks to pcfantasy for fix) (#66)
- Meta: Discord server was set up by FireController1847 - link in mod description
- Meta: Added project building instructions and PR review
- Steam: [Traffic Manager: President Edition (LABS)](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE LABS 1.10.15, 10/02/2019

- Added: (Experimental) Turn on red (thanks to FireController1847 for implementation and to pcfantasy for source code base)
- Added: Notification if user is still subscribed to old original TM:PE
- Improved: Use Escape key to close Traffic Manager without returning to Pause Menu (thanks to aubergine10 for suggestion) (#16)
- Improved: New icons for cargo and passenger train restriction (thanks to aubergine10) (#17)
- Updated: Updated pathfinding with missing vanilla logic
- Updated: Tweaked values in `CargoTruckAI` path finding (thanks to pcfantasy for improvement suggestion)
- Updated: Tweaked speed multiplier of reckless drivers to get more realistic speed range (thanks to aubergine10 for suggestion) (#23)
- Updated: Simplified Chinese translation updated (thanks to Emphasia for translating)
- Steam: [Traffic Manager: President Edition (LABS)](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)

#### TM:PE LABS 1.10.14, 27/01/2019

- Fixed: Added missing Car AI type (`postVanAI`) - now post vans and post trucks are assigned to service vehicles group
- Fixed: Vehicles doesn't stop when driving through toll booth - fixes toll booth income too
- Fixed: Cargo Airport doesn't work (Cargo planes not spawning and not arriving)
- Fixed: Mod Options layout (text label overlaps slider control if too wide)
- Updated: Compatible with C:SL 1.11.1-f2
- Updated: Polish translation
- Updated: Korean translation (thanks to Toothless FLY [ROK]LSh.st for translating)
- Meta: Krzychu1245 takes over development of TM:PE; project moves to new GitHub repo
- Steam: [Traffic Manager: President Edition (LABS)](https://steamcommunity.com/sharedfiles/filedetails/?id=1637663252)
- Maintainer: Krzychu1245 (GitHub user krzychu124)
- GitHub: [krzychu124/Cities-Skylines-Traffic-Manager-President-Edition](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition)

#### C:SL 1.11.1-f2 (Holiday Surprise Patch), 13/12/2018

- Fixed: Cargo planes circle and use buildings that do not otherwise have any plane traffic
- Fixed: Vehicles can't cross the center line of Small Industry roads
- Fixed: Various bugs in game and DLCs

#### TM:PE 1.10.13, 31/10/2018

- Fixed: Toll booth not working
- Meta: Roads United Core also breaks toll booths
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.11.0-f3 (Industries), 23/10/2018

- Added: Toll booths
- Added: Postal service, vans and trucks
- Added: Additional industry vehicles
- Added: Cargo airport and planes
- Added: Warehouses and storage buildings
- Fixed: Bugs in various DLCs

#### TM:PE 1.10.12, 12/08/2018

- Added: Allow/disallow vehicles to enter a blocked junction at transition and pedestrian crossing nodes (#195)
- Fixed: Emergency vehicles pass closed barriers at level crossings
- Fixed: Bus lines render U-turn where they should not (#207)
- Fixed: Parking AI - Cims leaving the city despawn their car at public transport stations (#214)
- Fixed: Crossing restrictions do not work at intersection between road and highway (#212)
- Updated: Compatible with C:SL 1.11.0-f3
- Updated: Bent nodes do not allow for U-turns by default (#170)
- Updated: Russian translation (thanks to vitalii2011 for translating)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.10.11-hotfix, 22/07/2018

- Updated: Bus lines render U-turn where they should not (#207)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.10.11, 21/07/2018

- Updated: U-turn lane connections are represented by appropriate lane arrow (#201)
- Fixed: Heavy vehicles are unable to U-turn at dead ends (#194)
- Fixed: Routing & Priority rules do not work properly for acute (< 30°)/obtuse(> 150°) segment angles (#199)
- Fixed: Buses do not prefer lanes with correct lane arrow (#206)
- Fixed: Race condition in path-finding might cause paths to be assigned to wrong vehicle/citizen (#205)
- Fixed: Vehicles are unable to perform U-turns when setting off on multi-lane roads (#197)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.10.10, 14/07/2018

- Improved: Parking AI - Improved park & ride behaviour
- Fixed: Parking AI causes unnecessary path-findings (#183, thanks to Sipke82 for reporting)
- Fixed: Prohibiting cims from crossing the road also affect paths where crossing is unnecessary (#168, thanks to aubergine10 for reporting)
- Updated: Parking AI - Walking paths from parking position to destination building take public transportation into account
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.10.9, 13/07/2018

- Updated: Compatible with C:SL 1.10.1-f3
- Updated: Re-implemented path-finding algorithm
- Updated: French translation (thanks to mjm92150 for translating!)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.10.1-f3, 04/07/2018

- Fixed: Various bugs in game and DLCs

#### TM:PE 1.10.8, 01/07/2018

- Added: Button to remove parked vehicles (in options dialog, see maintenance tab)
- Fixed: Parking AI - Cims spawn pocket cars when they originate from an outside connection
- Fixed: Incorrect speed limits returned for pedestrian lanes
- Fixed: Routing is not updated while the game is paused (thanks to Oh My Lawwwd! for reporting)
- Fixed: Vanilla traffic lights are ignored when either the priority signs or timed traffic light features are disabled (thanks to aubergine10 for reporting)
- Fixed: Park maintenance vehicles are not recognized as service vehicles
- Fixed: Cars leaving city state "thinking of a good parking spot" (thanks to aubergine10 for reporting)
- Updated: Parking AI - Removed check for distance between parked vehicle and target building
- Updated: Korean translation (thanks to Toothless FLY [ROK]LSh.st for translating)
- Updated: Polish translation (thanks to Krzychu1245 for translating)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.10.7, 28/05/2018

- Fixed: U-turn routing is inconsistent on transport lines vs. bus paths (#137, thanks to Zorgoth for reporting this issue)
- Fixed: Junction restrictions for pedestrian crossings are sometimes not preserved (#142, thanks to Anrew and wizardrazer for reporting this issue)
- Fixed: Geometry subscription feature may cause performance issues (#145)
- Fixed: Parking AI: Transport mode storage causes performance issues during loading (#147, thanks to hannebambel002 and oneeyets for reporting and further for providing logs and save games)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.10.6, 24/05/2018

- Added: Lane connector can be used on monorail tracks
- Added: Mod option - Main menu size can be controlled
- Added: Mod option - GUI and overlay transparency can be controlled
- Added: Mod option - Penalties for switching between different public transport lines can be toggled
- Added: Cims can now be removed from the game
- Improved: Advanced Vehicle AI - Tuned parameters
- Improved: Randomization for realistic speeds such that vehicles may change their target velocity over time
- Improved: Vehicle position tracking
- Improved: Mod compatibility checks
- Improved: Parking AI - Improved behaviour in situations where vehicles are parked near public transport hubs and road connections are partially unavailable
- Improved: Window design
- Fixed: Parking AI - Not all possible paths are regarded during path-finding
- Fixed: Parking AI - Cims become confused when trying to return their abandoned car back home (thanks Wildcard-25 for reporting and fixing!)
- Fixed: Parking AI - Cims do not search for parking building when road-side parking spaces are found
- Fixed: Parking AI - Parked vehicles are spawned near the source building even when cims are already en-route
- Fixed: Parking AI - Cims sometimes get stuck in an infinite loop while trying to enter their parked car
- Fixed: Lane connector does not work for roads with more than ten lanes
- Fixed: Allowing/Disallowing vehicles to enter a blocked junction does not work for certain junctions
- Updated: Compatible with C:SL 1.9.2-f1
- Updated: Compatible with C:SL 1.9.3-f1
- Updated: Compatible with C:SL 1.10.0-f3
- Updated: Dynamic Lane Selection: Absolute speed measurements are used instead of relative measurements
- Updated: Service vehicles now allowed to ignore lane arrows when leaving their source building; better for dead-end roads with median
- Updated: Korean translation (thanks to Toothless FLY [ROK]LSh.st for translating)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.10.0-f3 (Park Life), 24/05/2018

- Added: Park maintenance service and vehicle
- Added: Walking tours
- Added: Sightseeing bus tours and depot
- Added: Hot air balloons
- Fixed: Confused ships rotating forever
- Fixed: All traffic use Bus & Taxi lane when Old Town policy is active
- Fixed: Lots of other game and DLC bugs
- Updated: Trees reduce noise pollution

#### C:SL 1.9.3-f1, 23/03/2018

- Fixed: Bugs caused by prior patch

#### C:SL 1.9.2-f1, 09/03/2018

- Fixed: Minor bugs

#### TM:PE 1.10.5-hotfix, 07/01/2018

- Fixed: Monorail traffic lights do not show up (thanks merlineandrews for reporting)
- Improved: Moved "Removew this vehicle button"
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.10.5, 06/01/2018

- Added: Randomization for lane changing costs
- Added: Randomization for "trucks prefer innermost lanes on highways" costs
- Added: path-finding costs for public transport transitions
- Fixed: Main menu button might be out of view
- Fixed: Division by zero occurs for low speed roads
- Fixed: Automatic pedestrian lights at railroad do not work as expected
- Fixed: Timed traffic lights show up for bicycles (they should not)
- Fixed: Due to a multi-threading issue junction restrictions may cause the game state to become inconsistent
- Fixed: Routing rules prevents vehicles from spawning when starting building lies too close to an intersection/road end
- Fixed: Disabling tutorial message has no effect
- Fixed: "Stay on lane" feature does not work as intended for certain nodes
- Updated: Compatible with C:SL 1.9.1
- Updated: Busses are allowed to switch multiple lanes after leaving a bus stop
- Updated: Pedestrian traffic lights do not show up if crossing the street is prohibited
- Updated: Simplified Chinese translation updated (thanks to Emphasia for translating)
- Updated: Polish translation updated (thanks to Krzychu1245 for translating)
- Removed: Unnecessary calculations in path-finding
- Removed: UI scaling
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.9.1, 05/12/2017

- Fixed: Various game bugs
- Updated: Updated Unity to 5.6.4p2

#### TM:PE 1.10.4, 19/10/2017

- Added: Possibility to add priority signs at multiple junctions at once (press Shift)
- Added: Tutorials (can be disabled in the options window globally)
- Updated: Compatible with C:SL 1.9.0-f5
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.10.3-hotfix, 19/10/2017

- Fixed: Vehicle-separated traffic lights do not show up for bus lanes (thanks to Dafydd for reporting)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.9.0-f5 (Green Cities), 19/10/2017

- Added: Biofuel busses and recycling trucks
- Added: Electric cars and parking spaces with chargers
- Fixed: Huge number of game and localisation bugs
- Updated: Noise Pollution overhaul
- Updated: Train track intersection rules
- Updated: Unity version has been updated to 5.6.3p4

#### TM:PE 1.10.3, 18/08/2017

- Fixed: Setting unlimited speed limit causes vehicles to crawl at low speed (thanks to sethisuwan for reporting this issue)
- Fixed: Vehicle-separated traffic lights do not show up for trams & monorails (thanks to thecitiesdork for reporting this issue)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.10.2, 17/08/2017

- Improved: performance
- Fixed: Pedestrians sometimes ignore red traffic light signals (thanks to (c)RIKUPI™ for reporting this issue)
- Fixed: Timed traffic lights do not correctly recognize set vehicle restrictions (thanks to alborzka for reporting this issue)
- Updated: Compatible with C:SL 1.8.0-f3
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.8.0-f3 (Concerts), 17/08/2017

- Added: Festival areas
- Fixed: Pathfinder causing problems with multiple policies
- Fixed: Traffic routes info view does not show bicycles for players who don't own After Dark
- Fixed: Ships can travel on land and through dam
- Fixed: Cargo Train Terminal not working when build next to road with bicycle lanes
- Fixed: Large number of other game bugs

#### TM:PE 1.10.1, 05/08/2017

- Fixed: Default routing is disabled if the lane connector is used on a subset of all available lanes only
- Fixed: Parking AI cannot be enabled/disabled
- Fixed: Lane connection points can connected to themselves
- Updated: Polish, Korean, and Simplified Chinese translations
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.10.0, 30/07/2017

- Added: Dynamic Lane Selection
- Added: Adaptive step switching
- Added: Individual vehicles may be removed from the game
- Added: Mod option - Vehicle restrictions aggression
- Added: Mod option - Vehicles follow priority rules at junctions with timed traffic lights
- Added: Path-find statistics label
- Added: Confirmation dialog for "Clear Traffic" button
- Improved: Path-finding performance
- Improved: Traffic measurement engine performance
- Improved: Currently active timed traffic light step is remembered
- Improved: Disabling the Parking AI triggers graceful clean up procedure
- Improved: Vehicle state tracking
- Improved: Parking AI - Vehicles can now find parking spaces at the opposite road side
- Improved: Parking AI - Included an improved fallback logic for some edge cases
- Improved: Parking AI - Citizens should now be more successful in returning their cars back home  
- Improved: Parking AI - Tuned parking radius parameters
- Improved: Parking AI - If the limit for parked vehicles is reached and parking fails due to it, no alternative parking space is queried
- Improved: Vehicle AI - Busses prefer lanes with correct lane arrow over incorrect ones
- Fixed: Workaround for a base game issue that causes trams to get stuck
- Fixed: Using the bulldozer tool might lead to inconsistent road geometry information
- Fixed: Citizens that fail to approach their parked car fly towards their target building
- Fixed: Parking AI: Path-finding fails if cars are parked too far away from a road
- Fixed: Parking AI: Citizens approaching a car start to float away
- Fixed: "Heavy vehicles prefer outer lanes on highways" does not work
- Fixed: The lane connector does not allow connecting all available lane end points at train stations and on bidirectional one-lane train tracks
- Fixed: Vehicles may get stuck in several situations
- Updated: Compatible with C:SL 1.7.2-f1
- Updated: Upgrading to a road with bus lanes now copies an already existing traffic light state to the new traffic light
- Updated: Adding a vehicle separate light to a timed traffic lights applies the main light configuration
- Updated: Vehicles use queue skipping to prioritize path-finding runs that are caused by road modifications
- Updated: Trains do not longer stop in front of green timed traffic lights
- Updated: Relocated some mod options
- Updated: It is now possible to connect train station tracks and outside connections with the lane connector
- Updated: Trains do not wait for each other anymore near timed traffic lights
- Updated: The option "Road condition has a bigger impact on vehicle speed" is only shown if the Snowfall DLC is owned
- Updated: Reorganized global configuration file (sorry, your main menu and main button positions are reset)
- Updated: The flow/wait calculation mode to be used is now configurable via the global configuration file
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.7.2-f1, 01/06/2017

- Fixed: Multiple public transport stops at the same location causing division by zero / crashing the UI
- Fixed: Double clicking creating multiple stops at the same place
- Fixed: Various other bugs

#### TM:IAI 1.3.9-final, 29/05/2017

- Fixed: Traffic flow statistics
- Fixed: Monorail pathfinding
- Fixed: Save/load lane connector data
- Meta: This was the final release of TM:IAI
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:PE 1.9.6-hotfix, 28/05/2017

- Fixed: Cable cars are unable to turn around at end-of-line stations
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.9.6, 28/05/2017

- Fixed: Vehicles cannot perform U-turns at junctions with only one outgoing segment (thanks to Sunbird for reporting this issue)
- Fixed: Path-finding costs for large distances exceed the maximum allowed value (thanks to Huitsi for reporting this issue)
- Fixed: Under certain circumstances path-finding at railroad crossings allow switching from road to rail tracks.
- Updated: Simplified Chinese translation
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.9.5, 24/05/2017

- Improved: Language can now be switched without requiring a game restart
- Fixed: Routing calculation does not work as expected for one-way roads with tram tracks (thanks to bigblade66, Battelman2 and AS_ for reporting and providing extensive information)
- Fixed: Copying timed traffic lights causes timed traffic lights to be omitted during the save process (thanks to jakeroot and t1a2l for reporting this issue)
- Fixed: In certain situations unnecessary vehicle-separate traffic lights are being created
- Fixed: Upgrading a train track segment next to a timed traffic light causes trains to ignore the traffic light
- Fixed: Hotfix - Cable cars despawn at end-of-line stations
- Updated: Compatible with C:SL 1.7.1-f1
- Updated: Polish, Korean and Italian translation
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.9.4-hotfix, 23/05/2017

- Fixed: Cable cars despawn at end-of-line stations
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.9.4, 23/05/2017

- Added: Mod option - Ban private cars and trucks on bus lanes
- Improved: Optimized path-finding
- Fixed: Path-finding is unable to calculate certain paths after modifying the road network
- Updated: Increased path-finding cost for private cars driving on bus lanes
- Updated: Increased path-finding cost for disregarding vehicle restrictions
- Updated: Spanish and French translation
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.7.1-f1, 23/05/2017

- Fixed: Minor bugs

#### TM:PE 1.9.3, 22/05/2017

- Improved: Modified junction restrictions come into effect instantaneously
- Fixed: AI: Segment traffic data is not taken into account
- Fixed: Priority rules are not properly obeyed
- Fixed: Under certain circumstances priority signs cannot be removed
- Fixed: Path-finding is unable to calculate certain paths
- Updated: UI - Saving a timed step does not reset the timed traffic light to the first state
- Removed: Default vehicle restrictions from bus lanes
- Removed: Disabled notification of route recalculating because some players report crashes
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:IAI 1.3.9, 21/05/2017

- Fixed: Compatibility with C:SL 1.7.0-f5
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:PE 1.9.2, 20/05/2017

- Improved: UI - Main menu & UI tools performance improved
- Fixed: Traffic lights can be removed from junctions that are controlled by a timed traffic light program
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.9.1, 19/05/2017

- Fixed: Using the vanilla traffic light toggling feature crashes the game if TMPE's main menu has not been opened at least once
- Fixed: AI - More car traffic and less public transportation present than in vanilla
- Updated: French, Dutch and Korean translation
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.9.0-hotfix, 18/05/2017

- Improved: Removed an unnecessary error message log
- Fixed: Highway specific rules are broken (thanks Ronjoe for reporting)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.9.0, 18/05/2017

- Added: Parking restrictions
- Added: Speed limits can be set up for individual lanes with the Control key
- Added: Added timed traffic light and speed limit support for monorails
- Added: Copy & paste for individual timed traffic lights
- Added: Rotate individual timed traffic lights
- Added: Lane customizations may come into effect instantaneously
- Added: Mod option - Main button position can be locked
- Added: Mod option - Main menu position can be locked
- Added: Mod option - Added language selection in options dialog
- Added: Mod option - Customization of lane arrows, lane connections and vehicle restrictions can now come into effect instantaneously
- Added: Support for custom languages
- Added: Korean translation (thanks to Toothless FLY [ROK]LSh.st for translating)
- Improved: Performance improvements
- Improved: Advanced Vehicle AI - Algorithm updated, performance improved - Possible routing decisions are now being pre-calculated
- Improved: AI - Tuned path-finding parameters
- Fixed: Cars sometimes get stuck forever when the Advanced Parking AI is activated (thanks to cmfcmf for reporting this issue)
- Fixed: Busses do not perform U-turns even if the transport line show U-turns (thanks to dymanoid for reporting this issue)
- Fixed: Timed traffic lights do not work as expected on single-direction train tracks (thanks to DaEgi01 for reporting this issue)
- Fixed: Vehicle restriction and speed limit signs overlay is displayed on the wrong side of inverted road segments
- Fixed: Influx statistics value is zero (thanks to hjo for reporting this issue)
- Updated: Compatible with C:SL 1.7.0-f5
- Updated: Major code refactoring
- Updated: UI - More compact, movable main menu UI
- Updated: translations: German, Polish, Russian, Portuguese, Traditional Chinese
- Updated: Path-finding cost multiplicator for vehicle restrictions is now configurable in TMPE_GlobalConfig.xml
- Updated: Unified traffic light toggling feature with game code
- Updated: Reworked the way that traffic measurements are performed
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.7.0-f5 (Mass Transit), 18/05/2017

- Added: Ferries
- Added: Cable Cars
- Added: Elevated monorail
- Added: Blimps
- Added: New stations, transport hubs and service buildings
- Added: Named routes
- Added: One-way train tracks
- Added: Emergency vehicles choose free lane if available, otherwise lane with least traffic
- Added: More public transport info views
- Added: Choose if rail stations accept intercity traffic
- Added: Stop signs at intersections
- Added: Toggle traffic lights at intersections
- Added: Vehicles have show / hide routes button
- Added: Automatic public transport vehicle unbunching
- Fixed: Train tracks could be updated after disaster by upgrading
- Fixed: Helicopters still won't visit buildings with no road connection
- Fixed: Lots of errors, particularly localisation
- Updated: Increased emergency vehicles speed
- Updated: Smaller outside connection capacity for smaller roads
- Updated: Upgraded to Unity 5.5.3f1

#### TM:PE 1.8.16-hotfix, 21/03/2017

- Fixed: Trams were using regular roads.
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.16, 20/03/2017

- Improved: lane selection for busses if the option "Busses may ignore lane arrows" is activated
- Fixed: The game sometimes freezes when using the timed traffic light tool
- Fixed: Lane connections are not correctly removed after modifying/removing a junction
- Fixed: Selecting a junction for setting up junction restrictions toggles the currently hovered junction restriction icon
- Updated: Lane connections can now also be removed by pressing the backspace key
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:IAI 1.3.8, 05/03/2017

- Improved: Removed another unnecessary file
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:IAI 1.3.7, 05/03/2017

- Improved: Code clean-up, removed unnecessary files
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:IAI 1.3.6, 05/03/2017

- Improved: Moved buttons away from Natural Disasters panel
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:PE 1.8.15, 27/01/2017

- Updated: Compatible with C:SL 1.6.3-f1
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.6.3-f1, 26/01/2017

- Fixed: Helicopter not used if building has no road connection
- Fixed: Various other game bugs

#### TM:PE 1.8.14-hotfix, 07/01/2017

- Fixed: Manual traffic lights do not work properly (thanks dpitch40 for reporting)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.14, 07/01/2017

- Added: Tram lanes can now be customized by using the lane connector tool
- Improved: Minor performance optimizations for priority sign simulation
- Fixed: Wait/flow ratio at timed traffic lights is sometimes not correctly calculated
- Fixed: A deadlock situation can arise at junctions with priority signs such that no vehicle enters the junction
- Fixed: When adding a junction to a timed traffic light, sometimes light states given by user input are not correctly stored
- Fixed: Joining two timed traffic lights sets the minimum time to "1" for steps with zero minimum time assigned
- Fixed: Modifications of timed traffic light states are sometimes not visible while editing the light (but they are applied nonetheless)
- Fixed: Button background is not always correctly changed after clicking on a button within the main menu
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.13, 05/01/2017

- Improved: Selection of overlay markers on underground roads (thanks to Padi for reminding me of that issue)
- Improved: Minor performance improvements
- Fixed: Timed traffic light data can become corrupt when upgrading a road segment next to a traffic light, leading to faulty UI behaviour (thanks to Brain for reporting this issue)
- Fixed: The position of the main menu button resets after switching to the free camera mode (thanks to Impact and gravage for reporting this issue)
- Fixed: A division by zero exception can occur when calculating the average number of waiting/floating vehicles
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.12, 02/01/2017

- Fixed: After leaving the "Manual traffic lights" mode the traffic light simulation is not cleaned up correctly (thanks to diezelunderwood for reporting this issue)
- Fixed: Insufficient access rights to log file causes the mod to crash
- Updated: Compatible with C:SL 1.6.2-f1
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.11, 02/01/2017

- Fixed: Speed limits for elevated/underground road segments are sometimes not correctly loaded (thanks to Pirazel and \[P.A.N] Uf0 for reporting this issue)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.10, 31/12/2016

- Improved: Path-finding performance (a bit)
- Fixed: Check for invalid road thumbnails in the "custom default speed limits" dialog
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.9, 29/12/2016

- Added: It is now possible to set speed limits for metro tracks
- Added: Custom default speed limits may now be defined for train and metro tracks
- Improved: Customizable junctions are now highlighted by the lane connector tool
- Improved: UI behaviour
- Improved: Performance improvements
- Fixed: Selecting a junction to set up priority signs sometimes does not work (thanks to Artemis *Seven* for reporting this issue)
- Fixed: Automatic pedestrian lights do not work as expected at junctions with incoming one-ways and on left-hand traffic maps
- Updated: Junction restrictions may now be controlled at bend road segments
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.8, 25/12/2016

- Fixed: Taxis are not being used
- Fixed: Prohibiting U-turns with the junction restriction tool does not work (thanks to Kisoe for reporting this issue)
- Fixed: Cars are sometimes floating across the map while trying to park (thanks to [Delta ²k5] for reporting this issue)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.7-hotfix, 24/12/2016

- Fixed: Taxis are not being used
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.7, 24/12/2016

- Added: Italian translation (thanks to Admix for translating)  
- Improved: Advanced AI: Improved lane selection
- Improved: Overall user interface performance
- Improved: Overlay behaviour
- Improved: Traffic measurement
- Improved: Auto pedestrian lights at timed traffic lights behave more intelligently now
- Fixed: Parking AI - Cims that try to reach their parked car are sometimes teleported to another location where they start to fly through the map in order to reach their car
- Fixed: Parking AI - Cims owning a parked car do not consider using other means of transportation
- Fixed: Parking AI - Residents are unable to leave the city through a highway outside connection
- Fixed: Trains/Trams are sometimes not detected at timed traffic lights
- Updated: Compatible with C:SL 1.6.2-f1
- Updated: The position of the main menu button is now forced inside screen bounds on startup
- Updated: A timed traffic light step with zero minimum time assigned can now be skipped automatically
- Updated: Using the lane connector to create a U-turn now automatically enables the "U-turn allowed" junction restriction
- Updated: French translation (thanks to simon.royer007 for translating)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.6.2-f1, 21/12/2016

- Fixed: Various errors in game

#### TM:PE 1.8.6, 12/12/2016

- Added: Korean language (thanks to Toothless FLY [ROK]LSh.st for translating)
- Updated: Chinese language code (zh-cn -> zh) in order to make it compatible with the game (thanks to Lost丶青柠 for reporting this issue)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.5, 11/12/2016

- Fixed: Average speed limits are not correctly calculated for road segments with bicycle lanes (thanks to Toothless FLY [ROK]LSh.st for reporting this issue)
- Removed: "Evacuation busses may only be used to reach a shelter" (CO fixed this issue)
- Updated: Compatible with C:SL 1.6.1-f2
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.4, 11/12/2016

- Added: "Stay on lane" - Press Shift + S in the Lane Connector tool can cycle through lane directions.
- Fixed: Bicycles cannot change from bicycle lanes to pedestrian lanes
- Fixed: Travel probabilities set in the "Citizen Lifecycle Rebalance v2.1" mod are not obeyed (thanks to informmanuel, shaundoddmusic for reporting this issue)
- Fixed: Number of tourists seems to drop when activating the mod (statistics were not updated, thanks to hpp7117, wjrohn for reporting this issue)
- Fixed: When loading a second save game a second main menu button is displayed (thanks to Cpt. Whitepaw for reporting this issue)
- Fixed: While path-finding is in progress vehicles do "bungee-jumping" on the current segment (thanks to mxolsenx, Howzitworld for reporting this issue)
- Fixed: Cims leaving the city search for parking spaces near the outside connection which is obviously not required   
- Updated: U-turns are now only allowed to be performed from the innermost lane     
- Updated: TMPE now detects if the number of spawned vehicles is reaching its limit (16384). If so, spawning of service/emergency vehicles is prioritized over spawning other vehicles.
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.6.1-f2, 11/12/2016

- Added: Missing service enumerators to modding API
- Fixed: Relocating emergency shelter breaks evacuation route
- Fixed: Citizens using evacuation routes like a bus route
- Fixed: Lots of other bug fixes to game and Disasters DLC
- Updated: Chinese localisation added

#### TM:PE 1.8.3, 4/12/2016

- Improved: Tweaked U-turn behaviour
- Improved: Info views
- Fixed: Despite having the Parking AI activated, cims sometimes still spawn pocket cars.
- Fixed: When the Parking AI is active, bicycle lanes are not used (thanks to informmanuel for reporting this issue)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.2, 3/12/2016

- Fixed: Taxis were not used (thanks to [Delta ²k5] for reporting)
- Fixed: Minor UI fix in Default speed limits dialog
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.1, 1/12/2016

- Fixed: Mod crashed when loading a second save game
- Updated: translations: Polish, Chinese (simplified)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TPP2 2.0.12, 30/11/2016

- Updated: Compatible with C:SL 1.6.0-f4
- Meta: This was the final release of TPP2
- Meta: TM:PE continued as the main traffic mod for the game
- Meta: The TPP/TPP2 can still be found in the Network Extensions 2 project
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:PE 1.8.0-hotfix, 29/11/2016

- Updated: Reactivated Rush Hour interoperability
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.8.0, 29/11/2016

- Added: Default speed limits
- Added: Parking AI (replaces "Prohibit cims from spawning pocket cars")
- Added: Main menu button is now moveable
- Added: Mod option - Heavy vehicles prefer outer lanes on highways
- Added: Mod option - Realistic speeds
- Added: Mod option - Evacuation busses may ignore traffic rules (Natural Disasters DLC required)
- Added: Mod option - Evacuation busses may only be used to reach a shelter (Natural Disasters DLC required)
- Added: Traffic info view shows parking space demand if Parking AI is activated
- Added: Public transport info view shows transport demand if Parking AI is activated
- Added: Info texts for citizen and vehicle tool tips if Parking AI is activated
- Improved: AI - Improved lane selection, especially on busy roads
- Improved: AI - Improved mean lane speed measurement
- Updated: Compatible with C:SL 1.6.0-f4
- Updated: Extracted internal configuration to XML configuration file
- Updated: Changed main menu button due to changes in the game's user interface
- Updated: Translations for German, Portuguese, Russian, Dutch, Chinese (traditional)
- Removed: Compatibility check for Traffic++ V2 due to excessive workload
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.6.0-f4 (Natural Disasters), 29/11/2016

- Added: Disasters
- Added: Disaster Recovery Service (Van and Helicopter)
- Added: Police / Fire / Ambulance Helicopters
- Added: Pumping Service & Trucks
- Added: Emergency Shelter & Evacuation Bus
- Added: Additional policies (eg. Helicopter Priority)
- Added: Ability for roads to be destroyed by disasters

#### TM:PE 1.7.15, 26/10/2016

- Fixed: Timed traffic lights window disappears when clicking on it with the middle mouse button (thanks to Nexus and Mariobro14 for helping me identifying the cause of this bug)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.14, 18/10/2016

- Updated: Compatible with C:SL 1.5.2-f3
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.13, 15/09/2016

- Added: Button to reset stuck vehicles/cims (see mod settings menu)
- Fixed: Implemented a permanent fix to solve problems with stuck vehicles/cims caused by third party mods
- Fixed: AI: Lane merging was not working as expected
- Fixed: Pedestrian light states were sometimes not being stored correctly (thanks to Filip for pointing out this problem)
- Updated: AI - Improved lane selection algorithm
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.12, 09/09/2016

- Fixed: Timed traffic lights should now correctly detect trains and trams
- Fixed: GUI: Junction restriction icons sometimes disappear
- Updated: AI - Lane changes are reduced on congested road segments
- Updated: Chinese (simplified) translation
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.11, 01/09/2016

- Updated: Compatible with C:SL 1.5.1-f3
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.10-hotfix2, 01/09/2016

- Fixed: Manual pedestrian traffic light states were not correctly handled
- Fixed: Junction restrictions overlay did not show all restricted junctions
- Fixed: Setting up vehicle restrictions affects trams (thanks chem for reporting)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.10-hotfix1, 31/08/2016

- Improved: Rainfall compatibility
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.10, 31/08/2016

- Added: Players can now disable spawning of pocket cars
- Fixed: Timed traffic lights were flickering
- Fixed: Pedestrian traffic lights were not working as expected
- Fixed: When upgrading/removing/adding a road segment, nearby junction restrictions were removed
- Fixed: Setting up vehicle restrictions affects trams (thanks to chem for reporting)
- Fixed: Manual pedestrian traffic light states were not correctly handled
- Fixed: Junction restrictions overlay did not show all restricted junctions
- Updated: Chinese (simplified) translation
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.9-hotfix, 22/08/2016

- Fixed: Cims were not using public transport
- Fixed: Cims are not moving in
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.9, 22/08/2016

- Improved: Performance improvements
- Fixed: In-game traffic light states are now correctly rendered when showing "yellow"
- Fixed: GUI - Traffic light states do not flicker anymore
- Removed: Negative effects on public transport usage
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.8, 18/08/2016:

- Fixed: Cims sometimes got stuck (thanks to all reports and especially to Thilawyn for providing a savegame)
- Improved: GUI - Better traffic light arrow display
- Improved: Performance while saving
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.7, 16/08/2016:

- Added: "110" speed limit
- Improved: Performance while saving
- Improved: GUI - Windows are draggable
- Improved: GUI - Improved window scaling on lower resolutions
- Updated: AI - Instead of walking long distances, citizens now use a car
- Updated: AI - Citizens will remember their last used mode of transport (e.g. they will not drive to work and come return by bus anymore)
- Updated: AI - Increased path-finding costs for traversing over restricted road segments
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.6, 14/08/2016:

- Added: Players may now prohibit cims from crossing the street
- Added: the possibility to connect train track lanes with the lane connector (as requested by pilot.patrick93)
- Improved: UI - Clicking with the secondary mouse button now deselects the currently selected node/segment for all tools
- Improved: AI - Tuned randomization of lane changing behaviour
- Fixed: AI: At specific junctions, vehicles were not obeying lane connections correctly (thanks to Mariobro14 for pointing out this problem)
- Fixed: AI: Path-finding costs for U-turns were not correctly calculated (thanks to Mariobro14 for pointing out this problem)
- Fixed: Vehicles were endlessly waiting for each other at junctions with certain priority sign configurations
- Fixed: AI: Lane changing costs corrected
- Updated: AI - Introduced path-finding costs for leaving main highway (should reduce amount of detours taken)
- Updated: Moved options from "Change lane arrows" to "Vehicle restrictions" tool
- Updated: Russian translation
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.5, 07/08/2016:

- Fixed: AI - Cims were using pocket cars whenever possible
- Fixed: AI - Path-finding failures led to much less vehicles spawning
- Fixed: AI - Lane selection at junctions with custom lane connection was not always working properly (e.g. for Network Extensions roads with middle lane)
- Fixed: While editing a timed traffic light it could happen that the traffic light was deleted
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.4, 31/07/2016:

- Added: French translations (thanks to simon.royer007 for translating!)
- Improved: AI - Tuned new parameters
- Improved: Various code improvements
- Fixed: Activated/Disabled features were not loaded correctly
- Fixed: AI - At specific junctions the lane changer did not work as intended
- Fixed: Possible fix for OSX performance issues
- Updated: AI - Switched from relative to absolute traffic density measurement
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.3, 29/07/2016:

- Added: Ability to enable/disable mod features (e.g. for performance reasons)
- Improved: Further code improvements
- Fixed: Vehicle type determination was incorrect (fixed u-turning trams/trains, stuck vehicles)
- Fixed: Clicking on a train/tram node with the lane connector tool caused an error (thanks to noaccount for reporting this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.2, 26/07/2016:

- Improved: Optimized UI overlay performance
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.1, 24/07/2016:
- Fixed: Trains were not despawning if no path could be calculated
- Fixed: Workaround for third-party issue: TM:PE now detects if the calculation of total vehicle length fails    
- Removed: "Busses now may only ignore lane arrows if driving on a bus lane"
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.7.0, 23/07/2016:

- Added: Traffic++ lane connector
- Added: Compatibility detection for the Rainfall mod
- Fixed: Busses now may only ignore lane arrows if driving on a bus lane
- Improved: performance of priority sign rules
- Improved: Better UI performance if overlays are deactivated
- Improved: Better fault-tolerance of the load/save system
- Fixed: Taxis were allowed to ignore lane arrows
- Fixed: AI - Highway rules on left-hand traffic maps did not work the same as on right-hand traffic maps
- Fixed: Upgrading a road segment next to a timed traffic light removed the traffic light leading to an inconsistent state (thanks to ad.vissers for pointing out this problem)
- Updated: AI - Cims now ignore junctions where pedestrian lights never change to green
- Updated: AI - Removed the need to define a lane changing probability
- Updated: AI - Tweaked lane changing parameters
- Updated: AI - Highway rules are automatically disabled at complex junctions (= more than 1 incoming and more than 1 outgoing roads)
- Updated: Simulation accuracy now also controls time intervals between traffic measurements
- Updated: Default wait-flow balance is set to 0.8
- Updated: Rewritten and simplified vehicle position tracking near timed traffic lights and priority signs for performance reasons
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.22-hotfix, 29/06/2016:

- Fixed: Traffic measurement at timed traffic lights was incorrect
- Updated: AI - Taxis now may not ignore lane arrows and are using bus lanes whenever possible (thanks to Cochy for pointing out this issue)
- Updated: AI - Busses may only ignore lane arrows while driving on a bus lane
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.22, 21/06/2016:

- Improved: Advanced Vehicle AI - Improved lane selection at junctions where bus lanes end
- Improved: Advanced Vehicle AI - Improved lane selection of busses
- Improved: Speed/vehicle restrictions may now be applied to all road segments between two junctions by holding the shift key
- Improved: Automatic pedestrian lights
- Improved: Separate traffic lights: Traffic lights now control traffic lane-wise
- Fixed: Lane selection on maps with left-hand traffic was incorrect
- Fixed: While building in pause mode, changes in the road network were not always recognized causing vehicles to stop/despawn
- Fixed: Police cars off-duty were ignoring lane arrows
- Fixed: If public transport stops were near a junction, trams/busses were not counted by timed traffic lights (many thanks to Filip for identifying this problem)
- Fixed: Trains/Trams were sometimes ignoring timed traffic lights (many thanks to Filip for identifying this problem)
- Fixed: Building roads with bus lanes caused garbage, bodies, etc. to pile up
- Updated: Reworked how changes in the road network are recognized
- Updated: Sensitivity slider is only available while adding/editing a step or while in test mode
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.21, 14/06/2016:

- Fixed: Too few cargo trains were spawning (thanks to Scratch, toruk_makto1, Mr.Miyagi, mottoh and Syparo for pointing out this problem)       
- Fixed: Vehicle restrictions did not work as expected (thanks to nordlaser for pointing out this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.20, 11/06/2016:

- Fixed: Priority signs were not working correctly (thanks to mottoth, Madgemade for pointing out this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.19, 11/06/2016

- Fixed: Timed traffic lights UI not working as expected (thanks to Madgemade for pointing out this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TPP2 2.0.11, 10/06/2016

- Updated: Compatible with C:SL 1.5.0-f4
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:PE 1.6.18, 09/06/2016

- Improved: Players can now select elevated rail segments/nodes
- Improved: Trams and trains now follow priority signs
- Improved: performance of priority signs and timed traffic lights
- Improved: UI behaviour when setting up priority signs
- Updated: Compatible with C:SL 1.5.0-f4
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.5.0-f4 (Match Day), 09/06/2016

- Added: Football stadium (causes heavy traffic in city on match day)
- Fixed: Various bugs in game and DLCs

#### TPP2 2.0.10, 02/06/2016

- Fixed: `NullPointerException`
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TPP2 2.0.9, 31/05/2016

- Updated: Compatible with Network Extensions 2.5
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:PE 1.6.17, 20/04/2016

- Fixed: Hotfix for reported path-finding problems
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.16, 19/04/2016

- Updated: Compatible with C:SL 1.4.1-f2
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.4.1-f2, 19/04/2016

- Fixed: Busses could enter/exit bus stations at highways with sound barriers
- Fixed: Various other bugs

#### TPP2 2.0.8, 22/03/2016

- Updated: Code from TM:PE 1.6.10
- Updated: Compatible with C:SL 1.4.0-f3
- Removed: Old code from TPP
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:PE 1.6.15, 22/03/2016

- Added: Traditional Chinese translation
- Improved: Possible fix for crashes described by cosminel1982
- Updated: Compatible with C:SL 1.4.0-f3
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.4.0-f3, 22/03/2016

- Fixed: Lots of bugs in game and DLCs
- Updated: Lots of stuff in game and DLCs

#### TM:PE 1.6.14, 17/03/2016

- Fixed: Cargo trucks did not obey vehicle restrictions (thanks to ad.vissers for pointing out this problem)
- Fixed: When Advanced AI was deactivated, U-turns did not have costs assigned
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.13, 16/03/2016

- Added: Dutch translation
- Improved: The pedestrian light mode of a traffic light can now be switched back to automatic
- Improved: Vehicles approaching a different speed limit change their speed more gradually
- Improved: Path-finding performance improvements
- Improved: Fine-tuned path-finding lane changing behaviour
- Fixed: After loading another savegame, timed traffic lights stopped working for a certain time
- Fixed: Lane speed calculation corrected
- Updated: The size of signs and symbols in the overlay is determined by screen resolution height, not by width
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:IAI 1.3.5, 05/03/2016

- Fixed: Issues with tram lines
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:PE 1.6.12, 03/03/2016

- Improved: Reduced memory usage
- Fixed: Adding/removing junctions to/from existing timed traffic lights did not work (thanks to nieksen for pointing out this problem)
- Fixed: Separate timed traffic lights were sometimes not saved (thanks to nieksen for pointing out this problem)
- Fixed: Fixed an initialization error (thanks to GordonDry for pointing out this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.11, 03/03/2016

- Added: Chinese translation
- Added: By pressing "Page up"/"Page down" you can now switch between traffic and default map view
- Improved: Size of information icons and signs is now based on your screen resolution
- Updated: UI code refactored
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.10, 02/03/2016

- Added: Additional controls for vehicle restrictions added
- Fixed: Clicking on a Traffic Manager overlay resulted in vanilla game components (e.g. houses, vehicles) being activated
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.9, 02/03/2016

- Updated: Compatibility with C:SL 1.3.2-f1
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:IAI 1.3.4, 02/03/2016

- Fixed: Compatibility with C:SL 1.3.2-f1
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### C:SL 1.3.2-f1, 02/03/2016

- Fixed: Stuck pedestrian issues
- Fixed: Short roads warning
- Fixed: Cyclists despawn when changing from bike path to bike lane

#### TPP2 2.0.7, 01/03/2016

- Improved: Performance
- Updated: Vehicle AIs
- Updated: Code from TM:PE 1.6.7 merged in to TPP2
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:PE 1.6.8, 01/03/2016

- Added: Spanish translation
- Improved: Major path-finding performance improvements
- Updated: Japanese translation (thanks to Akira Ishizaki for translating!)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TPP2 2.0.6, 28/02/2016

- Updated: Cargo trucks pathfinder
- Updated: Stations and ports pathfinder
- Updated: Code from TM:PE 1.6.6 & 1.6.7 merged in to TPP2
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:PE 1.6.7, 27/02/2016

- Improved: Tuned AI parameters
- Improved: Traffic density measurements
- Improved: Lane changing near junctions - reintroduced costs for lane changing before junctions
- Improved: Vehicle behaviour near blocked roads (e.g. while a building is burning)
- Fixed: Automatic pedestrian lights for outgoing one-ways fixed
- Fixed: U-turns did not have appropriate costs assigned
- Fixed: The time span between AI traffic measurements was too high
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.6, 27/02/2016

- Improved: Easier to select segment ends in order to change lane arrows.
- Fixed: U-turning vehicles were not obeying the correct directional traffic light (thanks to t1a2l for pointing out this problem)
- Updated: Priority signs now cannot be setup at outgoing one-ways.
- Updated: French translation (thanks to simon.royer007 for translating!)
- Updated: Polish translation (thanks to Krzychu1245 for translating!)
- Updated: Portuguese translation (thanks to igordeeoliveira for translating!)
- Updated: Russian translation (thanks to FireGames for translating!)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TPP2 2.0.5, 26/02/2016

- Improved: Service vehicle pathfinding
- Fixed: Cars disappearing underground
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TPP2 2.0.4, 24/02/2016

- Improved: Vehicle restrictions
- Updated: Removed typecasting from Car AI
- Updated: Major pathfinder refactor
- Updated: Code from TM:PE 1.6.0 to 1.6.5 merged in to TPP2
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:PE 1.6.5, 24/02/2016

- Added: Despawning setting to options dialog
- Improved: Detection of Traffic++ V2
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TPP2 2.0.3, 23/02/2016

- Added: Medium pedestrianised road
- Fixed: Busses not using custom pathfinder
- Fixed: Crash bug when Snowfall DLC subscribed
- Fixed: Trams not working
- Fixed: Transport lines not respecting restrictions
- Fixed: Broken bus lanes on roads
- Fixed: Path finder not respecting vehicle types
- Fixed: Car AI broken when using Snowfall DLC
- Fixed: Bug loading options
- Updated: Improved how extended vehicle types are defined
- Updated: Custom path manager refactored
- Updated: Namespace and project refactor & clean-up
- Updated: Separate extensions and AIs
- Updated: Improved options definition and storage
- Removed: Ghost Mode
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:PE 1.6.4, 23/02/2016

- Improved: Minor performance improvements
- Fixed: Path-finding calculated erroneous traffic density values
- Fixed: Cims left the bus just to hop on a bus of the same line again (thanks to kamzik911 for pointing out this problem)
- Fixed: Despawn control did not work (thanks to xXHistoricalxDemoXx for pointing out this problem)
- Fixed: State of new settings was not displayed correctly (thanks to Lord_Assaultーさま for pointing out this problem)
- Fixed: Default settings for vehicle restrictions on bus lanes corrected
- Fixed: Pedestrian lights at railway junctions fixed (they are still invisible but are derived from the car traffic light state automatically)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.3.1-f1, 23/02/2016

- Fixed: Minor bug fixes and updates

#### TM:PE 1.6.3, 22/02/2016

- Fixed: Using the "Old Town" policy led to vehicles not spawning
- Fixed: Planes, cargo trains and ship were sometimes not arriving
- Fixed: Trams are not doing U-turns anymore
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TPP2 2.0.2, 21/02/2016

- Fixed: Bus routing & bus stops
- Fixed: European theme incompatible (thanks BloodyPenguin for help!)
- Fixed: Traffic doesn't stop for flooded tunnels
- Updated: Serialise for lane data
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:PE 1.6.2, 20/02/2016

- Added: Trams are now obeying speed limits (thanks to Clausewitz for pointing out the issue)
- Fixed: Clear traffic sometimes throwed an error
- Fixed: Vehicle restrictions did not work as expected (thanks to [Delta ²k5] for pointing out this problem)
- Fixed: Transition of automatic pedestrian lights fixed
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.6.1, 20/02/2016

- Improved: Performance
- Improved: Modifying mod options through the main menu now gives an annoying warning message instead of a blank page.
- Fixed: Various UI issues
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TPP2 2.0.1, 19/02/2016

- Fixed: Default vehicle restrictions always include bus
- Updated: Compatible with C:SL 1.3.0-f4
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:IAI 1.3.3, 19/02/2016

- Fixed: Compatibility with C:SL 1.3.0-f4
- Improved: Various things
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TPP2 2.0.0, 18/02/2016

- Added: Allow restrictions on pedestrianised roads
- Fixed: Bus lines not working
- Fixed: Traffic++ roads
- Fixed: Options screen now saves options properly
- Improved: Better compatibility with TPP
- Improved: Detection of incompatible mods
- Updated: Lots of code clean-up
- Meta: First sable release of TPP2
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TM:PE 1.6.0, 18/02/2016

- Added: Separate traffic lights for different vehicle types
- Added: Vehicle restrictions
- Added: "Vehicles may enter blocked junctions" may now be defined for each junction separately (again)
- Added: Road conditions (snow, maintenance state) may now have a higher impact on vehicle speed (see "Options" menu)
- Improved: Better handling of vehicle bans
- Improved: Method for calculating lane traffic densities
- Improved: Performance optimizations
- Improved: Advanced Vehicle AI: Improved lane spreading
- Fixed: Reckless drivers now do not enter railroad crossings if the barrier is down
- Fixed: Path-finding costs for crossing a junction fixed
- Fixed: Vehicle detection at timed traffic lights did not work as expected
- Fixed: Not all valid traffic light arrow modes were reachable
- Updated: Compatible with C:SL 1.3.0-f4
- Updated: Ambulances, fire trucks and police cars on duty are now ignoring lane arrows
- Updated: Timed traffic lights may now be setup at arbitrary nodes on railway tracks
- Updated: Option dialog is disabled if accessed through the main menu
- Updated: Vehicles going straight may now change lanes at junctions
- Updated: Vehicles may now perform U-turns at junctions that have an appropriate lane arrow configuration
- Updated: Emergency vehicles on duty now always aim for the fastest route
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### C:SL 1.3.0-f4 (Snowfall), 18/02/2016

- Added: Winter biome
- Added: Trams
- Added: Snow ploughs
- Added: Road maintenance
- Added: Road conditions affect vehicle speed
- Added: Priority routes

#### TPP2 0.0.1, 17/02/2016

- Added: Tool - Lane Connector (from TPP)
- Added: Tool - Vehicle Restrictions (from TPP)
- Added: Tool - Speed Restrictions (from TPP)
- Added: No Despawn (from TPP)
- Added: Improved AI (from TPP:AI)
- Meta: Released as version 0.0a
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)

#### TPP2 0.0, 17/02/2016

- Meta: Development started 4th October 2015 as part of ill-fated TAM project
- Meta: Was planned to replace TM:PE, TPP, etc
- Meta: Dev team: Katalyst6, LinuxFan, Lazarus*Man
- Steam: [Traffic++ V2](https://steamcommunity.com/sharedfiles/filedetails/?id=626024868)
- Maintainer: Katalyst6 (GitHub user Katalyst6)
- GitHub: [Katalyst6/CSL.TransitAddonMod](https://github.com/Katalyst6/CSL.TransitAddonMod)

#### TM:PE 1.5.2, 01/02/2016

- Added: Traffic lights may now be added to/removed from underground junctions
- Added: Traffic lights may now be setup at some points of railway tracks (there seems to be a game-internal bug that prevents selecting arbitrary railway nodes)
- Added: Display of priority signs, speed limits and timed traffic lights may now be toggled via the options dialog
- Fixed: Reckless driving does not apply for trains (thanks to GordonDry for pointing out this problem)
- Fixed: Manual traffic lights were not working (thanks to Mas71 for pointing out this problem)
- Fixed: Pedestrians were ignoring timed traffic lights (thanks to Hannes8910 for pointing out this problem)
- Fixed: Sometimes speed limits were not saved (thanks to cca_mikeman for pointing out this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.5.1, 31/01/2016

- Added: Trains are now following speed limits
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.5.0, 30/01/2016

- Added: Speed restrictions (as requested by Gfurst)
- Improved: AI - Parameters tuned
- Improved: Code improvements
- Fixed: Flowing/Waiting vehicles count corrected
- Updated: Lane arrow changer window is now positioned near the edited junction (as requested by GordonDry)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.4.9, 27/01/2016

- Added: Junctions can be added to/removed from timed traffic lights after they are created
- Improved: When viewing/moving a timed step, the displayed/moved step is now highlighted (thanks to Joe for this idea)
- Improved: Performance improvements
- Fixed: AI - Fixed a division by zero error (thanks to GordonDry for pointing out this problem)
- Fixed: AI - Near highway exits vehicles tended to use the outermost lane (thanks to Zake for pointing out this problem)
- Fixed: Some lane arrows disappeared on maps using left-hand traffic systems (thanks to Mas71 for pointing out this problem)
- Fixed: In lane arrow edit mode, the order of arrows was sometimes incorrect (thanks to Glowstrontium for pointing out this problem)
- Fixed: Lane merging in left-hand traffic systems fixed
- Fixed: Turning priority roads fixed (thanks to GordonDry for pointing out this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.4.8, 25/01/2016

- Added: Polish language (thanks to Krzychu1245 for working on this!)
- Added: Russian language (thanks to FireGames for working on this!)
- Improved: AI - Parameters have been tuned
- Improved: AI - Added traffic density measurements
- Improved: Performance improvements
- Fixed: After removing a timed or manual light the traffic light was deleted (thanks to Mas71 for pointing out this problem)
- Fixed: Segment geometries were not always calculated
- Fixed: In highway rule mode, lane arrows sometimes flickered
- Fixed: Some traffic light arrows were sometimes not selectable
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.4.7, 22/01/2016

- Added: Portuguese language added (thanks to igordeeoliveira for working on this!)
- Improved: Reduced file size (thanks to GordonDry for reporting this problem)
- Fixed: Freight ships/trains were not coming in (thanks to Mas71 and clus for reporting this problem)
- Fixed: The toggle "Vehicles may enter blocked junctions" did not work properly (thanks for exxonic for reporting this problem)
- Fixed: If a timed traffic light is being edited the segment geometry information is not updated (thanks to GordonDry for reporting this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.4.6, 22/01/2016

- Added: Running average lane speeds are measured now
- Fixed: Minor bug fixes
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.4.5, 22/01/2016

- Added: "Vehicles may enter blocked junctions" may now be defined for each junction separately
- Fixed: A deadlock in the path-finding is fixed
- Fixed: Small timed light sensitivity values (< 0.1) were not saved correctly
- Fixed: Timed traffic lights were not working for some players
- Updated: Refactored segment geometry calculation
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.4.4, 21/01/2016

- Added: Localization support
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.4.3, 20/01/2016

- Improved: Several performance improvements
- Improved: Calculation of segment geometries
- Improved: Load balancing
- Improved: Police cars, ambulances, fire trucks and hearses are now also controlled by the AI
- Fixed: Vehicles did not always take the shortest path
- Fixed: Vehicles disappeared after deleting/upgrading a road segment
- Fixed: Fixed an error in path-finding cost calculation
- Fixed: Outgoing roads were treated as ingoing roads when highway rules were activated
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.4.2, 16/01/2016

- Added: Option added to disable highway rules
- Improved: Several major performance improvements (thanks to sci302 for pointing out those issues)
- Improved: Saving/loading of timed traffic lights
- Fixed: AI did not consider speed limits/road types during path calculation (thanks to bhanhart, sa62039 for pointing out this problem)
- Fixed: Vehicles were stopping in front of green traffic lights
- Fixed: Stop/Yield signs were not working properly (thanks to GordonDry, Glowstrontium for pointing out this problem)
- Fixed: Cargo trucks were ignoring the "Heavy ban" policy, they should do now (thanks to Scratch for pointing out this problem)
- Updated: Lane-wise traffic density is only measured if Advanced AI is activated
- Meta: Connecting a city road to a highway road that does not supply enough lanes for merging leads to behaviour people do not understand (see manual).
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.4.1, 15/01/2016

- Fixed: Path-finding near junctions fixed
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.4.0, 15/01/2016

- Added: Advanced Vehicle AI (disabled by default! Go to "Options" and enable it if you want to use it.)
- Fixed: Traffic lights were popping up in the middle of roads
- Fixed: Fixed the lane changer for left-hand traffic systems (thanks to Phishie for pointing out this problem)
- Fixed: Traffic lights on invalid nodes are not saved anymore
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.24, 13/01/2016

- Added: Configuration option that allows vehicles to enter blocked junctions
- Improved: Priority signs: After adding two main road signs the next offered sign is a yield sign
- Improved: Priority signs: Vehicles now should notice earlier that they can enter a junction
- Fixed: Invalid (not created) lanes are not saved/loaded anymore
- Fixed: Some priority signs were not saved
- Fixed: Priority signs on deleted segments are now deleted too
- Fixed: Lane arrows on removed lanes are now removed too
- Fixed: Adding a priority sign to a junction having more than one main sign creates a yield sign (thanks to GordonDry for pointing out this problem)
- Fixed: If reckless driving was set to "The Holy City (0 %)", vehicles blocked intersections with traffic light.
- Fixed: Traffic light arrow modes were sometimes not correctly saved  
- Removed: Legacy XML file save system
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.23, 09/01/2016

- Added: Option added to forget all toggled traffic lights
- Fixed: Corrected an issue where toggled traffic lights would not be saved/loaded correctly (thanks to Jeffrios and AOD_War_2g for pointing out this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.22, 08/01/2016

- Added: Option allowing busses to ignore lane arrows
- Added: Option to display nodes and segments
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.21, 06/01/2016

- Added: Traffic Sensitivity Tuning
- Improved: When adding a new step to a timed traffic light the lights are inverted.
- Improved: Timed traffic light status symbols should now be less annoying
- Fixed: Deletion of junctions that were members of a traffic light group is now handled correctly
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.20, 04/01/2016

- Added: Reckless driving
- Improved: User interface
- Fixed: Timed traffic lights are not saved correctly after upgrading a road nearby
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.19, 04/01/2016
- Improved: Timed traffic lights: Velocity of vehicles is being measured to detect traffic jams
- Improved: Traffic flow measurement
- Improved: Path finding - Cims may now choose their lanes more independently
- Fixed: Upgrading a road resets the traffic light arrow mode
- Updated: Timed traffic lights: Absolute minimum time changed to 1
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.18, 03/01/2016

- Improved: You can now switch between activated timed traffic lights without clicking on the menu button again
- Fixed: Provided a fix for unconnected junctions caused by other mods
- Removed: Crosswalk feature removed. If you need to add/remove crosswalks please use the "Crossings" mod.
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.17, 03/01/2016

- Fixed: Timed traffic lights cannot be added again after removal, toggling traffic lights does not work (thanks to Fabrice, ChakyHH, sensual.heathen for pointing out this problem)
- Fixed: After using the "Manual traffic lights" option, toggling lights does not work (thanks to Timso113 for pointing out this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.16, 03/01/2016

- Fixed: Traffic light settings on roads of the Network Extensions mods are not saved (thanks to Scarface, martintech and Sonic for pointing out this problem)
- Improved: Save data management
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.15, 02/01/2016

- Added: Simulation accuracy (and thus performance) is now controllable through the game options dialog
- Fixed: Vehicles on a priority road sometimes stop without an obvious reason
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.14, 01/01/2016

- Improved: Performance
- Improved: Non-timed traffic lights are now automatically removed when adding priority signs to a junction
- Improved: Adjusted the adaptive traffic light decision formula (vehicle lengths are considered now)
- Improved: Traffic two road segments in front of a timed traffic light is being measured now  
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.13, 01/01/2016

- Fixed: Lane arrows are not correctly translated into path finding decisions (thanks to bvoice360 for pointing out this problem)
- Fixed: Priority signs are sometimes undeletable (thank to Blackwolf for pointing out this problem)
- Fixed: Errors occur when other mods without namespace definitions are loaded (thanks to Arch Angel for pointing out this problem)
- Fixed: Connecting a new road segment to a junction that already has priority signs now allows modification of the new priority sign
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.12, 30/12/2015

- Fixed: Priority signs are not editable (thanks to ningcaohan for pointing out this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.11, 30/12/2015

- Improved: Road segments next to a timed traffic light may now be deleted/upgraded/added without leading to deletion of the light
- Updated: Priority signs and Timed traffic light state symbols are now visible as soon as the menu is opened
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.10, 29/12/2015
- Fixed: an issue where timed traffic light groups were not deleted after deleting an adjacent segment
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.9, 29/12/2015

- Added: Introduced information icons for timed traffic lights
- Updated: Mod is now compatible with "Improved AI" (Lane changer is deactivated if "Improved AI" is active)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.8, 29/12/2015

- Improved: UI improvements
- Fixed: Articulated busses are now simulated correctly (thanks to nieksen for pointing out this problem)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.7, 28/12/2015

- Fixed: When setting up a new timed traffic light, yellow lights from the real-world state are not taken over
- Fixed: When loading another save game via the escape menu, Traffic Manager does not crash
- Fixed: When loading another save game via the escape menu, Traffic++ detection works as intended
- Fixed: Lane arrows are saved correctly
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.6, 28/12/2015

- Fixed: wrong flow value taken when comparing flowing vehicles
- Updated: Forced node rendering after modifying a crosswalk
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.5, 28/12/2015

- Improved: Adjusted the comparison between flowing (green light) and waiting (red light) traffic
- Fixed: Pedestrian traffic Lights (thanks to Glowstrontium for pointing out this problem)
- Fixed: Deleting a segment with a timed traffic light does not cause a `NullReferenceException`
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.4, 27/12/2015

- Improved: Better traffic jam handling
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.3, 27/12/2015

- Improved: Hotfix - Deleting a segment with a timed traffic light does not cause a `NullReferenceException`
- Updated: If priority signs are located behind the camera they are not rendered anymore
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.2, 27/12/2015

- Fixed: Synchronized traffic light rendering: In-game Traffic lights display the correct colour (Thanks to Fabrice for pointing out this problem)
- Fixed: Traffic lights switch between green, yellow and red. Not only between green and red.
- Updated: Priority signs are persistently visible when Traffic Manager is in "Add priority sign" mode
- Updated: UI tool tips are more explanatory and are shown longer.
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.1, 26/12/2015

- Fixed: Timed traffic lights of deleted/modified junctions get properly disposed
- Updated: Minimum time units may be zero now (timed traffic lights)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)

#### TM:PE 1.3.0, 25/12/2015

- Added: Features of TMPlus 1.2.0 (by sieggy and iMarbot)
- Added: Adaptive Timed Traffic Lights (automatically adjusted based on traffic amount)
- Steam: [Traffic Manager: President Edition](https://steamcommunity.com/sharedfiles/filedetails/?id=583429740)
- Maintainer: LinuxFan (GitHub user VictorPhilipp)
- GitHub: [VictorPhilipp/Cities-Skylines-Traffic-Manager-President-Edition](https://github.com/VictorPhilipp/Cities-Skylines-Traffic-Manager-President-Edition)

#### TMPlus 1.2.1, 15/12/2015

- Fixed: Recompiled to fix save errors
- Meta: This was the final release of TMPlus
- Steam: [Traffic Manager Plus 1.2.0](https://steamcommunity.com/sharedfiles/filedetails/?id=568443446)

#### TMPlus 1.2.0, 04/12/2015

- Updated: Game version 1.2.2-f2 compatible
- Meta: iMarbot takes over development of TMPlus
- Steam: [Traffic Manager Plus 1.2.0](https://steamcommunity.com/sharedfiles/filedetails/?id=568443446)
- Maintainer: iMarbot (GitHub user iMarbot)
- GitHub: [iMarbot/Skylines-Traffic-Manager-Plus](https://github.com/iMarbot/Skylines-Traffic-Manager-Plus)

#### TM:IAI 1.3.2, 02/12/2015

- Fixed: Update to use limits introduced in C:SL 1.2.2-f2
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### C:SL 1.2.2-f2, 05/11/2015

- Updated: Limits for zoning, buildings, road segments increased
- Fixed: Various bugfixes in game and asset editor

#### TM 1.0.9rc, 12/10/2015

- Removed: Tool - Pedestrian crosswalks (too buggy)
- Meta: Released as version 1.09rc
- Meta: Last release of original Traffic Manager mod by CBeTHaX (aka SvetlozarValchev)
- Steam: [Traffic Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TM 1.0.9rc, 12/10/2015

- Fixed: Minor bugs
- Meta: Released as version 1.09rc
- Steam: [Traffic Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TM 1.0.9rc, 12/10/2015

- Updated: Compatible with C:SL 1.2.1-f1
- Meta: Released as version 1.09rc
- Steam: [Traffic Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### C:SL 1.2.1-f1, 01/10/2015

- Fixed: Minor bugfixes with game and asset editor

#### TPP 1.6.1, 25/09/2015

- Removed: In-game log messages
- Meta: This was the last release of Traffic++ mod
- Meta: It was later continued in Traffic++ V2 (TPP2)
- Meta: It's features have since been merged in to TM:PE (tools & AIs) and NExt2 (roads)
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.6.0, 25/09/2015

- Fixed: Rendering of road customiser in underground view
- Updated: Integrated latest Improved Vehicle AI
- Updated: Lamps on pedestrian roads
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.5.6, 24/09/2015

- Fixed: Disable custom roads option not working
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.5.5, 24/09/2015

- Updated: Compatible with C:SL 1.2.0 (thanks javitonino)
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### C:SL 1.2 (After Dark), 24/09/2015

- Added: Bicycles
- Added: Bus and bike lanes
- Added: Prison Vans
- Added: Taxis

#### TM:IAI 1.3.1, 18/09/2015

- Improved: File size optimisation
- Fixed: Numerous bugs
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:IAI 1.3, 17/09/2015

- Added: Intersection editor from TPP (by jfarias)
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:IAI 1.2.2, 03/09/2015

- Fixed: Timed node groups not being saved
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:IAI 1.2.1, 02/09/2015

- Fixed: Null reference error when saving a city
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:IAI 1.2, 02/09/2015

- Added: GUI for changing parameters in-game
- Added: Toggle for TM and IAI lane interaction
- Added: No despawn feature by CBeThaX
- Improved: Verify data loaded from save game
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:IAI 1.1, 16/08/2015

- Fixed: But that prevented U-turns
- Improved: Compatibility with Mod Tools
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)

#### TM:IAI 1.0, 12/08/2015

- Added: TM 1.0.6 features (by CBeThaX)
- Added: Improved AI from TPP:AI 1.0.0 (by jfarias).
- Steam: [Traffic Manager + Improved AI](https://steamcommunity.com/sharedfiles/filedetails/?id=498363759)
- Maintainer: fadster (GitHub user fadster)
- GitHub: [/fadster/TrafficManager_ImprovedAI](https://github.com/fadster/TrafficManager_ImprovedAI)

#### TMPlus 1.1.6, 09/08/2015

- Fixed: Load/save system
- Fixed: Saving no longer opens dev console
- Steam: [Traffic Manager Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=481786333)

#### TM 1.0.6, 07/08/2015

- Updated: Revert to old version with C:S 1.1.1 compatibility
- Meta: Released as version 1.06
- Steam: [Traffic Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TMPlus 1.1.5, 06/08/2015

- Fixed: Null reference error when loading old save games
- Updated: New save system
- Updated: New load system
- Meta: Lots of code refactoring
- Steam: [Traffic Manager Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=481786333)

#### TPP:AI 1.0.0, 02/08/2015

- Added: Improved Vehicle AI (vehicles use all lanes, vehicles can change lanes)
- Meta: This was the first and last release of TPP:AI
- Steam: [492391912 - Improved AI Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=492391912)
- Maintainer: jfarias (GitHub user joaofarias)
- GitHub: N/A

#### TM 1.0.5, 28/07/2015

- Fixed: Pathfinder bugs
- Fixed: Lane changer and traffic remover features sometimes disabled even without Traffic++
- Fixed: Turn direction not matching lane marking logic (thanks klparrot!)
- Updated: Code refactor and clean-up
- Updated: Compatible with C:SL 1.1.1
- Meta: Released as version 1.05
- Steam: [Traffic Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TMPlus 1.1.4, 26/07/2015

- Fixed: Stop signs not working
- Steam: [Traffic Manager Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=481786333)

#### TMPlus 1.1.3, 15/07/2015

- Fixed: PathManager. Should now work for people without Traffic++
- Steam: [Traffic Manager Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=481786333)

#### TMPlus 1.1.2, 15/07/2015

- Updated: Reverted `PathFinder` as it's causing Null Reference exceptions. Need to refactor it.
- Steam: [Traffic Manager Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=481786333)

#### TMPlus 1.1.1, 14/07/2015

- Added: Features of TM 1.0.4 (by CBeTHaX)
- Added: Support for tunnels
- Fixed: Turn direction not matching lane marking logic (thanks klparrot!)
- Fixed: Compilation errors
- Improved: Custom AI code refactoring and clean-up
- Improved: Tool code refactoring and clean-up
- Improved: UI code refactoring and clean-up
- Updated: Compatible with C:SL 1.1.1
- Meta: First release of TMPlus
- Steam: [Traffic Manager Plus](https://steamcommunity.com/sharedfiles/filedetails/?id=481786333)
- Maintainer: sieggy (GitHub user sieggy)
- GitHub: [seiggy/Skylines-Traffic-Manager](https://github.com/seiggy/Skylines-Traffic-Manager)

#### TPP 1.5.4, 06/07/2015

- Updated: Compatible with C:SL 1.1.1
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### C:SL 1.1.1, 01/07/2015

- Added: Tunnels for pedestrian paths
- Added: Autosave feature
- Fixed: Lots of stuff
- Improved: Asset editor

#### TPP 1.5.3, 02/06/2015

- Fixed: Crash on Linux
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.5.2, 02/06/2015

- Fixed: Trucks not allowed on new roads
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.5.1, 01/06/2015

- Fixed: Bug preventing options loading if button not shown in content manager
- Fixed: Duplicate prefab bug in Ghost Mode
- Fixed: Duplicate roads in roads panel in Ghost Mode
- Fixed: Vehicles have wrong AI when returning to main menu
- Fixed: Vehicles using restricted lanes
- Fixed: Bug preventing the Scrollable Toolbar mod from working in the entirety of the speed customizer panel
- Updated: Added debug logs to the game's F7 Debug Panel (thanks Nefarion!)
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TM 1.0.4-hotfix2, 29/05/2015

- Fixed: Negative timers (thanks XaBBoK!)
- Improved: Pathfinder clean-up (thanks dornathal!)
- Steam: [Traffic Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TPP 1.5.0 hotfix, 28/05/2015

- Fixed: Vehicles stopping in road
- Fixed: Realistic speeds not stopping without restarting the game
- Fixed: Road costs (thanks Archomeda)
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.5.0, 27/05/2015

- Added: More roads
- Added: Option for Improved Vehicle AI
- Fixed: Bug causing crashes when loading map through pause menu
- Fixed: Tunnels from new roads turning in to normal tunnels
- Fixed: Custom vehicles ignoring lane restrictions
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.4.0 hotfix, 23/05/2015

- Fixed: Few more bugs
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.4.0 hotfix1, 23/05/2015

- Fixed: Wrong lane usage in highways
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.4.0, 22/05/2015

- Added: Tool - Speed Limits
- Added: Underground view for customisation tools
- Added: Mod Option - No Despawn (from TM:PE)
- Added: Multi-track station enabler
- Improved: New UI for road customisation tools
- Fixed: Road customisation tools appearing in asset editor
- Fixed: Objects under the map bug
- Fixed: Options button on all resolutions
- Fixed: Train tracks should not be selectable
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.3.2-hotfix2, 20/05/2015

- Improved: Compatibility with C:SL 1.1.0b
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.3.2-hotfix1, 19/05/2015

- Updated: Compatible with C:SL 1.1.0b
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TM 1.0.4-hotfix1, 19/05/2015

- Fixed: Support for tunnels in AIs and pathfinder
- Updated: Compatible with C:SL 1.1.0b
- Updated: Traffic++ Compatibility - No despawn or lane changer available in compatibility mode
- Steam: [Traffic Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### C:SL 1.1.0b, 19/05/2015

- Added: European theme
- Added: Tunnels for roads and rail
- Improved: Metro tunnels can be built at different heights

#### TPP 1.3.2, 05/05/2015

- Added: Ability to set restrictions lane by lane
- Added: Ability to customise multiple lanes at same time
- Fixed: Bug that prevented settings saving
- Fixed: Bug that prevented short roads being selected
- Fixed: Vehicle restrictions now show correctly on all resolutions
- Updated: Road customiser tool button moved
- Updated: Customisation overlays visible at all times when tool active
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.3.1, 29/04/2015

- Improved: Performance issues reduced
- Fixed: Strange vehicle behaviours
- Fixed: Prevent road tool from selecting vehicles
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.3.0, 28/04/2015

- Added: More roads
- Added: Services overlay (vehicle restrictions) for pedestrian roads
- Added: Tool - Vehicle Restrictions
- Added: Tool - Lane Connections
- Fixed: Left hand traffic issues
- Fixed: Ghost mode caused crash if busway bridges on map
- Fixed: Several crashes caused by mod incompatibilities
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TM 1.0.4 beta, 22/04/2015

- Added: Buttons for timed traffic light states: skip and move up/down
- Fixed: Null reference exceptions when placing a road
- Fixed: Pathfinder issues
- Improved: Lane changes
- Updated: Prevent lane changer being used on non-node segments
- Meta: Released as version 1.04rc
- Steam: [Traffic Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TM 1.0.1, 21/04/2015

- Fixed: Errors on very close segments added to timed traffic lights
- Fixed: Save/load traffic lights going negative (finally)
- Meta: Released as version 1.01rc
- Steam: [Traffic Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TM 1.0.0, 21/04/2015

- Fixed: Wrong lane change (finally)
- Fixed: Timed traffic lights should no longer go below zero
- Fixed: UI problems and positioning
- Meta: First release in Steam Workshop under name "Traffic Manager"
- Meta: Released as version 1.0rc
- Steam: [Traffic Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TLM 0.9.0, 21/04/2015

- Fixed: Cars and service stop working
- Fixed: Wrong lane use
- Updated: Manager no longer loads in map editor
- Fixed: Errors on adding additional segment add
- Fixed: Negative numbers for traffic lights
- Added: Thumbnail to workshop page
- Meta: New save id
- Meta: Released as version 0.9b
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TLM 0.8.5, 19/04/2015

- Fixed lane merges in Left Hand Traffic maps
- Meta: Versions 0.8.3 and 0.8.4 were skipped
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TLM 0.8.2, 19/04/2015

- Updated: Rename 'Manual Control' to 'Manual Traffic Lights'
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TLM 0.8.1, 19/04/2015

- Added: Tool - Toggle Traffic Lights
- Updated: No more deleting trains on 'clear traffic'
- Fixed: Null exception timed traffic lights on node upgrade
- Fixed: UI position on different resolutions
- Meta: Released as version 0.8b
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TLM 0.8.0, 19/04/2015

- Fixed: Can now load game from pause menu
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TLM 0.7.1, 18/04/2015

- Improved: Tuned car wait on non-priority road
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TLM 0.7.0, 18/04/2015

- Fixed: Lanes on save/load in Left Hand traffic maps
- Fixed: Lane positions
- Fixed: More save/load fixes
- Fixed: Various timed lights problems
- Fixed: Traffic Manager load on multiple game loads
- Fixed: Multiple saves on one save game
- Updated: Moved UI button
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TLM 0.6.1, 18/04/2015

- Fixed: Cars coming form wrong lane in Left Hand Traffic maps
- Fixed: Not being able to save on new game
- Fixed: Errors when pressing Esc before opening menu
- Meta: Released as version 0.61b
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TLM 0.6.0, 18/04/2015

- Added: Tool - Manual Control (traffic lights)
- Added: Tool - Priority Signs
- Added: Tool - Toggle Pedestrian Crossings
- Added: Tool - Clear Traffic
- Added: Tool - Vehicle Restrictions (Transport, Service, Cargo or Car)
- Added: Option - No Despawn (thanks cope)
- Added: Custom Pathfinder
- Added: Settings stored in save game
- Updated: Compatible with C:SL 1.0.7c
- Updated: Custom traffic light steps can be timed
- Updated: Custom Road AI
- Updated: Better traffic light UI
- Meta: Using cities-skylines-detour by cope (sschoener on github)
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### TPP 1.2.1, 16/04/2015

- Fixed: Bugs introduced by previous version
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.2.0, 14/04/2015

- Added: Realistic driving speeds
- Added: More roads
- Fixed: Bug in mod options screen
- Updated: Compatible with C:SL 1.0.7c
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### C:SL 1.0.7c, 07/04/2015

- Fixed: Lots of stuff

#### TPP 1.1.8, 05/04/2015

- Added: Support for left-hand traffic maps
- Improved: Compatibility with Fine Road Heights mod
- Fixed: Various bugs
- Updated: Compatible with C:SL 1.0.7
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### C:SL 1.0.7, 27/03/2015

- Fixed: Lots of stuff

#### TPP 1.1.7, 26/03/2015

- Fixed: Various bugs
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.1.6, 26/03/2015

- Fixed: Various bugs
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.1.5, 26/03/2015

- Fixed: Various bugs
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.1.4, 26/03/2015

- Fixed: Various bugs
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.1.3, 26/03/2015

- Fixed: Various bugs
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.1.2, 26/03/2015

- Fixed: Various bugs
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.1.1, 25/03/2015

- Added: Option to disable updates from previous version
- Fixed: Bugs from previous version
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TPP 1.1.0, 25/03/2015

- Added: More roads & associated features
- Added: Option to disable pedestrians in middle lane
- Meta: Renamed to "Traffic++"
- Steam: [Traffic++](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### CSLT 1.0.5, 24/03/2015

- Fixed: Options panel bug
- Steam: [CSL-Traffic](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### CSLT 1.0.4, 23/03/2015

- Fixed: Various bugs
- Steam: [CSL-Traffic](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### TLM 0.5.0, 22/03/2015

- Added: Tool - Traffic Lights Editor
- Added: Tool - Lane Changer
- Added: Toolbar for mod features
- Added: Log file
- Updated: Compatible with C:SL 1.0.6
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)

#### CSLT 1.0.3, 20/03/2015

- Added: Option to toggle which vehicles can use pedestrian roads
- Added: Ghost mode to disable most of mod but still allow maps to load
- Updated: Compatible with C:SL 1.0.6
- Meta: First working implementation of vehicle restrictions
- Steam: [CSL-Traffic](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### C:SL 1.0.6, 19/03/2015

- Fixed: Lots of stuff

#### CSLT 1.0.2, 18/03/2015

- Improved: Some interface improvements
- Steam: [CSL-Traffic](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### CSLT 1.0.1, 17/03/2015

- Fixed: Mod could only be used once per gaming session
- Fixed: Various bugs
- Steam: [CSL-Traffic](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)

#### CSLT 1.0.0, 16/03/2015

- Added: Pedestrian zoneable road
- Meta: Traffic++ project starts, but under its original name "CSL-Traffic"
- Steam: [CSL-Traffic](https://steamcommunity.com/sharedfiles/filedetails/?id=409184143)
- Maintainer: jfarias (GitHub user joaofarias)
- GitHub: [joaofarias/csl-traffic](https://github.com/joaofarias/csl-traffic)

#### TLM 0.4.0, 14/03/2015

- Meta: Traffic Manager project starts
- Steam: [Traffic Lights Manager](https://steamcommunity.com/sharedfiles/filedetails/?id=427585724)
- Maintainer: CBeTHaX (GitHub user SvetlozarValchev)
- GitHub: [SvetlozarValchev/Skylines-Traffic-Manager](https://github.com/SvetlozarValchev/Skylines-Traffic-Manager)

#### C:SL 1.0, 10/03/2015

- New: Cities: Skylines 1.0 official release
