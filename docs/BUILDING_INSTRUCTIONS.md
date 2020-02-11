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

#### GitHub and Windows Mandatory ASLR

Recent versions of Windows 10 include an anti-exploit mechanism called [Mandatory ASLR](https://msrc-blog.microsoft.com/2017/11/21/clarifying-the-behavior-of-mandatory-aslr/). However, this can prevent unix-like executable (such as those used by GitHub) from functioning.

If you have system-wide Mandatory ASLR enabled, you might see errors such as:

* `.... \git\usr\bin\sh.exe: *** fatal error - cygheap base mismatch detected`
* `.... \app\mingw64\libexec\git-core\git-submodule: line 1121: cmd_: command not found`

The only known workaround is to exclude all executables in `\resources\app\git\` from Mandatory ASLR. For details see [GitHub issue #3096](https://github.com/desktop/desktop/issues/3096#issuecomment-529138491).

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

This is only required for the JetBrains Rider IDE:

* [Activate Roslyn Analyzers](https://www.jetbrains.com/help/rider/Settings_Roslyn_Analyzers.html) ([why?](https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition/pull/463))

## Build

This will compile the source code to DLL files...

> If you get errors at this stage, it's likely that your IDE isn't able to find the managed `.dll` files it needs. See **Managed DLLs** section at bottom of this page.

#### Configurations:

There are several build configurations, but we mostly use just three:

* `DEBUG` - this build enables all kinds of debug features, including the in-game debug menu, debug overlays and debug logging
    * Mod display name will include `DEBUG`
* `RELEASE` - this build is for public releases of the `STABLE` edition of TM:PE
    * Mod display name will include `STABLE`
* `RELEASE LABS` - this build is for the public releases of the `LABS` edition of TM:PE
    * Mod display name will include `LABS`

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

#### Managed DLLs

> You only need to do this once, and only if your IDE wasn't able to find the required managed `.dll` files.

TM:PE requires references to several "managed" `.dll` files that are bundled with the game; it should automatically find the files, but if not you'll have to do some additional setup...

First, locate your `Steam` folder. It is normally found in:

* **Windows:** `C:\Program Files (x86)\Steam\`
* **Mac OS X:** `~/Library/Application Support/Steam/`

The managed `.dll` files are usually located within the following sub-folder: `\steamapps\common\Cities_Skylines\Cities_Data\Managed\`.

You'll have to link the dependencies using _only one_ of the following methods:

* Create a `\TLM\dependencies` folder (in the TM:PE repository folder) and copy in the `.dll` files
* Or, create a `\TLM\dependencies` symlink to the `...\Managed` folder shown above
* Or, manually add reference paths to each project in the solution.

If you choose the latter option, the procedure depends on your IDE.

In Visual Studio:

* Open `\TLM\TMPL.sln` in your IDE, then...
* For each project listed in the **Solution Explorer** (right sidebar):
    1. Right-click the project, then choose **Properties**
    2. Select **Reference Paths** on the left
    3. Add the full path to the `Managed` folder.

In JetBrains Rider:

* At time of writing, there is no GUI for adding Reference Paths so you have to do it manually:
    * For each project, create a `<project name>.csproj.user` file (in the folder of that project)
        * For example, the `TLM` project needs a `TLM.csproj.user` file (in the `\TLM` folder)
    * Paste in the following code, making sure to edit the path to the `Managed` folder:
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <PropertyGroup>
        <ReferencePath>C:\full\path\to\Managed\</ReferencePath>
    </PropertyGroup>
</Project>
```
