# Debug Symbols

When debugging CS mods, it would be useful to get file and line number in your exceptions stack trace like this:
```
Exception: test kian exception
  at NetworkDetective.Test.Factorial (Int32 n) [0x00025] in C:\Users\dell\source\repos\NetworkDetective\NetowrkDetective\NetworkDetectiveMod.cs:35 
  at NetworkDetective.Test.Factorial (Int32 n) [0x0000a] in C:\Users\dell\source\repos\NetworkDetective\NetowrkDetective\NetworkDetectiveMod.cs:32 
  at NetworkDetective.Test.Factorial (Int32 n) [0x0000a] in C:\Users\dell\source\repos\NetworkDetective\NetowrkDetective\NetworkDetectiveMod.cs:32 
  at NetworkDetective.Test.Factorial (Int32 n) [0x0000a] in C:\Users\dell\source\repos\NetworkDetective\NetowrkDetective\NetworkDetectiveMod.cs:32 
  at NetworkDetective.NetworkDetectiveMod.OnEnabled () [0x00001] in C:\Users\dell\source\repos\NetworkDetective\NetowrkDetective\NetworkDetectiveMod.cs:50 
```
Follow these steps to get file and line number (I am working to reduce these steps).

# Generating MDB file

mono cannot use pdb files. So we need to convert it to mdb file. Not every version of pdb2mdb.exe works for us. So follow these steps to convert pdb to mdb

## Install unity 5.6.7 

Download from [Unity website](https://unity3d.com/get-unity/download/archive?_ga=2.67679633.1897806993.1593985032-213511360.158022272)
![image](https://user-images.githubusercontent.com/26344691/86768847-d74ba200-c056-11ea-8cfd-98921b079923.png)

if you do not install unity in program files then create a `\TLM\Unity` [symlink](https://github.com/git-for-windows/git/wiki/Symbolic-Links) to it.

## Build
With Unity installed, building TMPE also generates the mdb file and copies it to the mod folder.

## Replace CO

I have modified CO dll (see https://github.com/kianzarrin/PedestrianBridge/issues/7#issuecomment-654776528) to automatically load `<FileName>.dll.mdb` if one exists in the same directory as `<FileName>.dll` . Download it here:
[ColossalManaged.zip](https://github.com/kianzarrin/PedestrianBridge/files/4884294/ColossalManaged.zip)
Copy this in place of your ColossalManaged.dll located in `C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed` (depending on your CS installation folder).
