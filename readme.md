# TES3Merge v0.8

[![.NET](https://github.com/NullCascade/TES3Merge/actions/workflows/TES3Merge.yml/badge.svg)](https://github.com/NullCascade/TES3Merge/actions/workflows/TES3Merge.yml)

This tool helps to automatically patch conflicts between mods for *The Elder Scrolls III: Morrowind*.

This program includes an INI file to allow customization. Check [TES3Merge.ini](TES3Merge/TES3Merge.ini) for details on object blacklisting/whitelisting, debug options, and object type toggles.

## Usage

Extract the TES3Merge folder into the Morrowind installation directory. It can work outside this directory, but when managing multiple installs, it will always look to the parent directory first to find Morrowind.

Simply run TES3Merge.exe, then activate the new Merged Objects.esp file.

If running a Russian, Polish, or Japanese install, see [TES3Merge.ini](TES3Merge/TES3Merge.ini) to specify file encoding.

## Further Details

For example, [Patch for Purists](https://www.nexusmods.com/morrowind/mods/45096/?) fixes the Expensive Skirt's name, while [Better Clothes](https://www.nexusmods.com/morrowind/mods/42262/?) provides alternative appearances. If you use the two mods together, the changes from one mod will be ignored. With object merging, the changes that both mods make can make it into the game. The following image demonstrates the resolved conflict:

![Example conflict resolution image](https://cdn.discordapp.com/attachments/381219559094616064/583192237450461187/unknown.png)

Currently, TES3Merge supports the following record types: Activator, Alchemy, Apparatus, Armor, Birthsign, Body, Book, Class\*, Clothing, Container, Creature, Door, Enchantment, Faction\*, GMST, Ingredient, Light, Lock, Magic Effect, Miscellaneous, NPC, Probe, Race\*, Repair Item, Skill, Sound, Sound Generator, Spell, Static, and Weapon. Types marked with a \* are incomplete merges, still favoring the last loader at all cost.

Merge rules respect the load order, with the first appearance of the record becoming the base for comparisons. If a later mod modifies the record, its changes will be preserved. Another mod after that will only have its changes made if they differ from the base record.

## Contributing and Credits

TES3Merge is written using C#, and makes use of the [TES3Tool](https://github.com/SaintBahamut/TES3Tool) library by [SaintBahamut](https://github.com/SaintBahamut). A fork of this dependency is cloned with this repo.

## License

TES3Merge is MIT licensed. See [LICENSE](LICENSE) for more information.
