# ModDifficultyLibrary
A library for 1.4 versions of tModLoader.

Makes implementing custom difficulties (akin to Expert and Master mode) simpler and more direct.

Specifically:

- Any classes which derive form `ModDifficulty` are registered as an available difficulty.
- Modded difficulties appear on the World select and World Creation screen alongside others.
- A world is considered to be of modded difficulty when `Main.GameMode` is `4`.
- You can check if the modded difficulty is the current one by calling:
   1) `ModDifficulty.IsModeActive<T>()` where `T` is the class name of the modded difficulty.
   2) `ModDifficulty.IsModeActive(string origin, string difficultyName)` for any modded difficulty

To add as a dependancy, add `modReferences = ModDifficultyLibrary` to your mod's `build.txt` file.

Currently `Mod.Call()` is not implemented as a way to interact with the library.

Oh, and if anybody's curious, yes- feel free to appropriate the code for your own terraria mod, or refer to the code for your own purposes.
