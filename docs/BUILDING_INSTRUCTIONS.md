

# Project building instructions


#### Prerequisites:
* [Git for Windows](https://gitforwindows.org/) / [GitHub Desktop](https://desktop.github.com/)
* one prefered __IDE__ _(Integrated Development Environment)_ to build project:
  * Visual Studio 2017 Community (free)
  * JetBrains Rider (paid)
  * or other similar...
* sources of this repository: 
  * Git for Windows console:
    * use ```git clone https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition``` to download sources of this repository
    * after successful cloning type ```cd Cities-Skylines-Traffic-Manager-President-Edition```
    * then ```git submodule update --init --recursive``` to fetch dependencies
  * Github desktop:
    * clone repository using this link ```https://github.com/krzychu124/Cities-Skylines-Traffic-Manager-President-Edition.git```
    * dependencies should be installed automatically
    
## To build project follow actions:


Open __TMPL.sln__ located at ``` <Your cloned source code folder>\TLM\``` using preferred __IDE__.

##### Visual Studio:

 * use dropdown from __actions bar__ _(dropdown located under Team menu)_ to select _desired_ solution configuration
 * fix missing libraries locations - inside __Solution Explorer__ right click on every project and select __Properties__
 then __Reference Paths__ and add __Managed__ folder from game directory located under ```<game_dir>\Cities_Data\Managed```
 * to build project with selected configuration choose one of actions:
   * right click on __TLM__ project from __Solution Explorer__
   * use _Build_ menu -> _Build Solution_ __F6__

##### JetBrains Rider:
 * currently there is no GUI for adding Reference Paths so you have to create config for every project inside from scratch: 
 
   1. Create file with extension ```*.csproj.user```named as project name e.g. ```TLM.csproj.user```
   2. Paste below code inside newly created file and replace ```<full_url_to_game_location>``` with correct path
   ```
    <?xml version="1.0" encoding="utf-8"?>
    <Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
      <PropertyGroup>
        <ReferencePath><full_url_to_game_location>\Cities_Data\Managed\</ReferencePath>
      </PropertyGroup>
    </Project> 
   ```
 
 * select configuration using dropdown located on _actions bar_
 * use __Ctrl+F9__ to build solution, or __right click__ on solution inside __File Explorer (Alt+1)__
