# TES3Merge

This tool helps to automatically patch conflicts between mods for *The Elder Scrolls III: Morrowind*.



## Usage

Extract the TES3Merge folder into the Morrowind installation directory. It can work outside this directory, but when managing multiple installs, it will always look to the parent directory first to find Morrowind.

Simply run TES3Merge.exe, then activate the new Merged Objects.esp file.

## Further Details

For example, [Patch for Purists](https://www.nexusmods.com/morrowind/mods/45096/?) fixes the Expensive Skirt's name, while [Better Clothes](https://www.nexusmods.com/morrowind/mods/42262/?) provides alternative apperances. If you use the two mods together, the changes from one mod will be ignored. With object merging, the changes that both mods make can make it into the game. The following image demonstrates the resolved conflict:

![Example conflict resolution image](https://cdn.discordapp.com/attachments/381219559094616064/583192237450461187/unknown.png)

Currently, TES3Merge supports the following record types:

* Activator
* Alchemy
* Apparatus
* Armor
* Book
* Clothing
* Container
* Creature
* Door
* GMST
* Miscellaneous
* NPC
* Weapon

Merge rules respect the load order, with the first appearance of the record becoming the base for comparisons. If a later mod modifies the record, its changes will be preserved. Another mod after that will only have its changes made if they differ from the base record.

## Contributing and Credits

TES3Merge is written using C#, and makes use of the [TES3Tool](https://github.com/SaintBahamut/TES3Tool) library by [SaintBahamut](https://github.com/SaintBahamut). A fork of this dependency is cloned with this repo.

Visual Studio 2017 is required to build the solution.

## License

TES3Merge is MIT licensed. See [LICENSE](LICENSE) for more information.
