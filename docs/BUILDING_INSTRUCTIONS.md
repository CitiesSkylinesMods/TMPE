# Solution building instructions

This guide explains how to:

* Install required software
* Clone the repository (source code) to your local computer
* Build (compile) the source code to DLL files
* Test in-game

We strongly recommend doing development work on Windows as there's much more tooling available. If you're using a Mac, set up a Boot Camp partition for Windows and use that. If you're using Linux, consult the internets.

## Install

> You only need to do these steps once

The following software is required...

* IDE (Integrated Development Environment), either:
    * [Visual Studio Community](https://visualstudio.microsoft.com/vs/) (free)
        * Important: [2019 version](https://docs.microsoft.com/en-us/visualstudio/releases/2019/release-notes) (or later) must be used ([why?](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/pull/463))
    * [JetBrains Rider](https://www.jetbrains.com/rider/) (paid)
* GitHub client, either:
    * [GitHub Desktop](https://desktop.github.com/) (recommended if you are not used to working with GitHub)
    * [Git for Windows](https://gitforwindows.org/)

If desired, there are some [additional tools listed in the wiki](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/wiki/Dev-Tools).

## Clone

> You only need to do these steps once

This will clone the source code to your local machine...

#### GitHub Desktop:

* Go to [the online repository](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition)
* Click the green **Clone or Download** button and choose **Open in Desktop**
* Follow on-screen instructions (dependencies will be handled automatically)

#### Git for Windows:

* Open the inbuilt console
* Use `git clone https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition` clone repository locally
* Then `cd Cities-Skylines-Traffic-Manager-President-Edition`
* Then `git submodule update --init --recursive` to fetch dependencies

## Environment

> You only need to do these steps once

Open `\TLM\TMPL.sln` in your IDE, then...

#### In Visual Studio:

* Reference paths must be added to each project that appers in the **Solution Explorer** on the right:
    * Right-click a project, then choose **Properties**
    * Select **Reference Paths** on the left
    * Add folder: `C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed\` (may be different folder depending on where game is installed)

#### In JetBrains Rider:

* [Activate Roslyn Analyzers](https://www.jetbrains.com/help/rider/Settings_Roslyn_Analyzers.html) ([why?](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/pull/463))
* At time of writing, there is no GUI for adding Reference Paths so you have to do it manually:
    * For each project, create a `<project name>.csproj.user` file (in the folder of that project)
        * For example, the `TLM` project needs a `TLM.csproj.user` file (in the `\TLM` folder)
    * Paste in the following code, editing the path if necessary:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <PropertyGroup>
        <ReferencePath>C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed\</ReferencePath>
    </PropertyGroup>
</Project> 
```

## Build

This will compile the source code to DLL files...

#### Configurations:

There are several build configurations, but we mostly use just three:

* `DEBUG` - this build enables all kinds of debug features, including the in-game debug menu, debug overlays and debug logging
* `RELEASE` - this build is for public releases of the `STABLE` edition of TM:PE
* `RELEASE LABS` - this build is for the public releases of the `LABS` edition of TM:PE

#### In Visual Studio:

* Choose desired build configuration
    * Use the drop-down on the toolbar (it's adjacent to `Any CPU`)
* Then build the project:
   * Right-click **TLM** project in **Solution Explorer** and choose **Build**
   * Or, **Build > Build Solution** (shortcut: `Ctrl` + `Shift` + `B`)

#### In JetBrains Rider:

* Choose desired build configuration
    * Use the dropdown on the actions bar (under Team menu)
* Then build the project:
   * Right-click **TLM** project in **Solution Explorer** and choose **Build**
   * Or, **Build > Build Solution** (shortcut: `F6`)

#### Testing:

The built DLL files are automatically copied over to the local mods folder of the game if the build is successful. If not, check the post-build events for the `TLM` project.

Once built, you should be able to test the mod in-game. Remember to enable it in **Content Manager > Mods**.

If you have any problems, let us know!