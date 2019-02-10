


## **Setting up debugger**

## NOTE:
## These operations can degrade game performance (even up to 50-70% loss of fps in larger cities - depends on your CPU) because of additional instuction calls due to enabled mono debugger!



### Requirements:

#### Go to [this repository](https://github.com/0xd4d/dnSpy/releases) and download two files:
* dnSpy-net472.zip
* Unity-debugging-5.x.zip


#### Next:
* open __Unity-debugging-5.x.zip__ and go to *"Unity-debugging\ __unity-5.6.6__ \win64\"* and extract *mono.dll* somewhere to your computer e.g. *Desktop*
* go to steam game folder which containing Cities Skylines and backup *mono.dll* located inside *"Cities_Data\Mono"!*
* copy extracted *mono.dll* to replace with original you've backed up
* run game as usual using Steam to check if it's working properly

#### If game is working properly you can continue, if not just replace mono.dll from your backup and forget about attaching debugger to this game :cry:

* close game for now
* add two **User env. variables** *(Win+R(Run) sysdm.cpl -> tab Advanced -> Env. Variables at the bottom)* :
   * __key:__ *DNSPY_UNITY_DBG*
   * __value:__ *--debugger-agent=transport=dt_socket,server=y,address=127.0.0.1:55555,defer=y,no-hide-debugger*

   * __key:__ *DNSPY_UNITY_DBG2*
   * __value:__ *--debugger-agent=transport=dt_socket,server=y,address=127.0.0.1:55555,suspend=n,no-hide-debugger*


* extract __dnSpy-net472.zip__ to any folder e.g. *dnSpy* *(no matter where)*
* open extracted folder and run __dnSpy.exe__
* from left __Assembly Explorer__ remove all dll's if any *(select one, hit __Ctrl+A__ and then __delete__)*

* run again game as usual and switch *(Alt+Tab)* to running __dnSpy__
* inside dnSpy IDE hit __F5__ or __Start__ button and select:
   * Debug Engine: __Unity (Connect)__
   * Port: __55555__
* now you can click __OK__
* after that you should see __orange status bar__ at the bottom of application with text: _"Running..."_
* now open __Modules__ tab (_Debug -> Windows -> Modules_ (__Ctrl+Shift+A__))
* if everything works properly you should see a lot of dll's and other "data-00..." names
* right click on any name then select __Open All Modules__ (game may hang for few seconds) and press __OK__
* now on the left side in Assembly Explorer you should see all dll's loaded in-game (note that there would be *duplicated names*)
* right click on any of those names and then __Sort Assemblies__ for easier managing

### Congratulations you've properly set up debugging workspace, now you can make your mods bugless with ease :wink:

#### ADDITIONAL NOTES:
* use __Search__ *(Ctrl+Shift+K)* tab for searching anything you want inside all loaded assemblies (class, property, field, method etc...)
* right click on method definition and select Analyze to check where it's used

#### MOAR OF ADDITIONAL NOTES:
* I don't know why we can see duplicated libs (maybe it's some sort of protection)but as you find out in a minute
only one of them will have working breakpoints (usually it's that first of two after sorting) and if you find out which is working you can safely remove the other from Assembly Explorer

* I don't recommend rebuilding your mod library with game running because after that you have to clear Assembly Explorer and open again Modules (and you will see even more duplicates of your dll)
