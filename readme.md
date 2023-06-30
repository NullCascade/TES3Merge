# TES3Merge v0.10

[![.NET](https://github.com/NullCascade/TES3Merge/actions/workflows/TES3Merge.yml/badge.svg)](https://github.com/NullCascade/TES3Merge/actions/workflows/TES3Merge.yml)

This tool helps to automatically patch conflicts between mods for _The Elder Scrolls III: Morrowind_.

This program includes an INI file to allow customization. Check [TES3Merge.ini](TES3Merge/TES3Merge.ini) for details on object blacklisting/whitelisting, debug options, and object type toggles.

## Usage

Extract the TES3Merge folder into a subfolder of the Morrowind installation directory. It can work outside this directory, but when managing multiple installs, it will always look to an ancestor directory first to find Morrowind.

Simply run TES3Merge.exe, then activate the new Merged Objects.esp file.

If running a Russian, Polish, or Japanese install, see [TES3Merge.ini](TES3Merge/TES3Merge.ini) to specify file encoding.

Additional command line parameter options are available, detailed below. The default behavior of TES3Merge is equivalent to `TES3Merge.exe --patches all`.

| Option                                      | Description                                                                     |
| ------------------------------------------- | ------------------------------------------------------------------------------- |
| `-i`, `--inclusive`                         | Merge lists inclusively per element (implemented for List<NPCO>).               |
| `--no-masters`                              | Do not add masters to the merged esp.                                           |
| `-r`, `--records <records>`                 | Merge only specified record types.                                              |
| `--ignore-records`, `--ir <ignore-records>` | Ignore specified record types                                                   |
| `-p`, `--patches <patches>`                 | Apply any of the patches detailed below. If left empty all patches are applied. |
| `--version`                                 | Show version information                                                        |
| `-?`, `-h`, `--help`                        | Show help and usage information                                                 |

### Patches

TES3Merge creates patches to solve common issue in mods. These are all enabled by default, but can be configured by passing the `-p` or `-patches` command line argument.

| Patch     | Description                                                                                                                                      |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| none      | No patches will be applied.                                                                                                                      |
| all       | All patches will be applied.                                                                                                                     |
| cellnames | Creates a patch to ensure renamed cells are not accidentally reverted to their original name.                                                    |
| fogbug    | This option creates a patch that fixes all fogbugged cells in your active plugins by setting the fog density of those cells to a non-zero value. |
| summons   | This option to the multipatch ensures that known summoned creatures are flagged as persistent.                                                   |

### Commands

Additionally, there are additional commands beside the default merge command. You can run them with `TES3Merge.exe multipatch` and `TES3Merge.exe verify`.

| Command                                     | Description                                                                     |
| ------------------------------------------- | ------------------------------------------------------------------------------- |
| `multipatch`                                | Create a multipatch that merges levelled lists and fixes various other bugs.    |
| `verify`                                    | Checks esps for missing file paths.                                             |

### Configuration

TES3Merge also contains a configuration file, [TES3Merge.ini](TES3Merge/TES3Merge.ini). Documentation for the config file can be found in the file itself.

## Further Details

As an example, [Patch for Purists](https://www.nexusmods.com/morrowind/mods/45096/?) fixes the Expensive Skirt's name, while [Better Clothes](https://www.nexusmods.com/morrowind/mods/42262/?) provides alternative appearances. If you use the two mods together, the changes from one mod will be ignored. With object merging, the changes that both mods make can make it into the game. The following image demonstrates the resolved conflict:

![Example conflict resolution image](https://cdn.discordapp.com/attachments/381219559094616064/583192237450461187/unknown.png)

Currently, TES3Merge supports the following record types: Activator, Alchemy, Apparatus, Armor, Birthsign, Body Part, Book, Cell, Class\*, Clothing, Container, Creature, Door, Enchantment, Game Setting, Ingredient, Leveled Creature, Leveled Item, Light, Lockpick, Magic Effect, Misc. Item, NPC, Probe, Race\*, Repair Tool, Skill, Sound, Sound Generator, Spell, Static, and Weapon. Types marked with a \* are incomplete merges, still favoring the last loader for parts it doesn't know how to merge.

Merge rules respect the load order, with the first appearance of the record becoming the base for comparisons. If a later mod modifies the record, its changes will be preserved. Another mod after that will only have its changes made if they differ from the base record.

## Contributing and Credits

TES3Merge is written using C#, and makes use of the [TES3Tool](https://github.com/SaintBahamut/TES3Tool) library by [SaintBahamut](https://github.com/SaintBahamut). A fork of this dependency is cloned with this repo.

## License

TES3Merge is MIT licensed. See [LICENSE](LICENSE) for more information.
