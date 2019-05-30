# TES3Merge Changelog

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
