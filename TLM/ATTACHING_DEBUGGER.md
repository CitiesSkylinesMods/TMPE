# Attaching Debugger to Cities.exe

Use this guide to attach a debugger to Cities: Skylines.

> **Notes:**
> * Attaching a debugger can significantly reduce frame rate and cause lots of lag.
> * This has only been tested on Windows.

### Setup

> You only need to follow these steps once to set up your debug environment.

First, let's backup your current ```mono.dll```:

* Navigate to ```<%STEAM%>\steamapps\common\Cities_Skylines\Cities_Data\Mono\```
* Make a backup of ```mono.dll``` (you could just rename it ```mono-backup.dll```)

> The location of ```<%STEAM%>``` is usually ```C:\Program Files (x86)\Steam```

Next, download the following files from [```https://github.com/0xd4d/dnSpy/releases```](https://github.com/0xd4d/dnSpy/releases):

* ```dnSpy-net472.zip```
* ```Unity-debugging-5.x.zip```

Now we apply a new ```mono.dll``` and test the game is working:

* Make sure the game is **not** running
* Open ```Unity-debugging-5.x.zip```:
    * Navigate to ```Unity-debugging\unity-5.6.6\win64\``` (note: **```unity-5.6.6```**)
    * Copy ```mono.dll``` to ```<%STEAM%>\steamapps\common\Cities_Skylines\Cities_Data\Mono\```
* Run the game to check if it's working:
    * If not, delete the downloaded ```mono.dll``` then restore the original version
    * You'll have to scour the internet to work out what went wrong, sorry.
* Close the game

Next, add two environment variables:

* Press **Win+R** (_Run dialog appears_):
    * Enter ```sysdm.cpl```
    * Choose **OK**
* On the **Advanced** tab, choose **Environment Variables...**
* The variables to add are shown below:

1. > **key:** ```DNSPY_UNITY_DBG```  
   > **value:** ```--debugger-agent=transport=dt_socket,server=y,address=127.0.0.1:55555,defer=y,no-hide-debugger```
2. > **key:** ```DNSPY_UNITY_DBG2```  
   > **value:** ```--debugger-agent=transport=dt_socket,server=y,address=127.0.0.1:55555,suspend=n,no-hide-debugger```

Finally, unarchive **dnSpy**:

* Extract the downloaded ```dnSpy-net472.zip``` to a folder
    * It can be anywhere, eg. ```dnSpy/``` on your desktop

### Debugging

> Do this each time you want to debug the game.

First, launch **dnSpy**:

* Run ```dnSpy.exe```
* On the left, in **Assembly Explorer**, remove any ```.dll``` files that are listed
    * Tip: Select one, press **Ctrl+A** then **Delete**

Now run the game and attach **dnSpy**:

* Start ```Cities.exe```
* **Alt+Tab** to **dnSpy** app
* Press **F5** (or choose **Start**) and select:
    * **Debug Engine:** ```Unity (Connect)```
    * **Port:** ```55555```
* Click **OK**:
    * You should see an **orange status bar** at the bottom of application with text: ```Running...```
* From the **Debug** menu, choose **Windows -> Modules** _(Ctrl+Shift+A)_
* You should see lots of ```.dll``` files and some ```data-00...``` entries
* **Right-click** on any of them, select **Open All Modules**, then * Click **OK**
    * The game may hang for few seconds
* On the left, in **Assembly Explorer**, you should see all ```.dll``` files loaded in-game
    * There will be some duplicates
* **Right-click** on any of them, then **Sort Assemblies** to make the list easier to work with

That's it, you are debugging. Now your mods are sure to be bugless :P 

### Reverting

If you want to return the game back to normal:

* Exit the game
* Replace the downloaded ```mono.dll`` with your backup of the original ```mono.dll```
* Start the game

I'm sure you can work out how to simplify or automate toggling between the two ```mono.dll``` files :)

### Tips

* Use **Search** tab _(Ctrl+Shift+K)_ for to find class, property, field, method, etc...
* You can right-click a method definition then select **Analyze** to see where it's used

### Notes

* I have no idea why there are duplicated libraries (some sort of protection?)
* Only one copy of each library will have working breakpoints
    * After sorting assemblies, it's usually the first instance of a listed file
    * Once you know which one it is, you can safely remove the other from Assembly Explorer
* Don't rebuild your mod library with game running, otherwise you'll have to clear Assembly Explorer and open the modules again, which means the duplicates come back
