# TES3Merge Changelog

## v0.7 (2020-12-20)

* Pulls in various updates to TES3Tool. This includes a fix to autocalculated NPCs.
* Added file filtering, to blacklist certain esm/esp files from merging.
* Disabled FACT merging entirely until there are cycles to fix it.

## v0.6.1 (2020-01-26)

* Fixed issue where generating merged objects with Merged Objects.esp active would freeze the program.

## v0.6 (2020-01-26)

This update primarily brings up changes in TES3Lib and adds support for more than 255 mods.

* Fixed compatibility with non-Windows environments (courtesy of Benjamin Winger). No Linux or macOS builds are provided.
* Fixed issue where blacklisting all record types would instead try to merge all record types, including unsupported types.
* Fixed CLAS.CLDT merging to ignore favored attributes/skills. These are no longer merged, and will need to be done manually in a future version.
* Fixed FACT.FADT merging to ignore favored attributes/skills as well as rank data. This could create a crash when approaching NPCs using this faction. See above note.
* Brought upstreem changes to underlying TES3Lib changes, merging in SaintBahamut's work. No expected change in merge behavior.
* Removed 255 mod limit.

## v0.5.1 (2019-06-08)

* Fixed issue that would force default flags onto creatures, resulting in evolved slaughterfish that could go onto land.
* Fixed issue with CREA.FNAM subrecord merging.

## v0.5 (2019-06-04)

* Added support for specifying a file encoding in TES3Merge.ini. This brings in support for Russian, Polish, and Japanese installs.
* Fixed handling of NPC/creature AI travel destinations, as well as escort/follow packages.

## v0.4 (2019-06-03)

* Added support for more record types: BSGN, CLAS, SNDG, SOUN, SPEL, STAT
* Added filter to ignore "Merged_Objects.esp" from other merge object tools.
* Fixed text encoding issues (again, maybe for real this time). Still restricted to Windows-1252 encoded content files.
* Changed record dumping to provide the raw loaded bytes, rather than TES3Merge's interpretation of them. Does not apply to the tool's output serialization. This will help with debugging future issues.
* Made stronger attempts to read invalid Morrowind.ini files.
* Fixed issue where factions with no attributes would error when serializing.

## v0.3 (2019-05-31)

This update merges in changes to Bahamut's TES3Tool to fix Windows-style quotes from becoming question marks.

* Fixed encoding issues. Note that encoding is restricted to English content files.
* Added support for more record types: BODY, FACT, MGEF, SKIL

## v0.2.1 (2019-05-30)

Hotfix to fix issue where ESM files were loaded prior to ESP files.

* Fixed issue where ESM files would take priority over ESP files.
* Fixed broken link in readme.

## v0.2 (2019-05-30)

* Improved merge logic.
* Added support for more record types to merge.
* Added support for ignoring certain record types.
* Added support for multi-layer object filtering via regex.
* Added debug option to dump merged record history to log.
* Added ini option to not pause after execution.
* Added Morrowind.ini file checking. TES3Merge will report about invalid entries in the ini file, but will still try to load it.
* Fixed generated file having invalid version/record counts in the header, which made tools like Enchanted Editor complain.
* Fixed issue where load order followed Morrowind.ini instead of also respecting file timestamps.
* Fixed issue where records were checked in reverse order, giving priority to earlier mods.

## v0.1 (2019-05-29)

* Initial release.
