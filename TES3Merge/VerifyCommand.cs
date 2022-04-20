using IniParser;
using IniParser.Model;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TES3Lib;
using TES3Lib.Base;
using static TES3Merge.Util;

namespace TES3Merge
{
    internal static class VerifyCommand
    {
        internal static Dictionary<string, List<string>>? extensionToFolderMap;

        /// <summary>
        /// Verifies all active esps in the current Morrowind directory
        /// Parses all enabled records of the plugin and checks paths if the file exists
        /// </summary>
        /// <param name="verify"></param>
        /// <exception cref="Exception"></exception>
        internal static void Verify(bool verify)
        {
            // Main execution attempt.
#if DEBUG == false
        try
#endif
            {
                // Load this application's configuration.
                {
                    var parser = new FileIniDataParser();
                    var iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TES3Merge.ini");
                    Util.Configuration = parser.ReadFile(iniPath);
                }

                // Determine what encoding to use.
                try
                {
                    var iniEncodingCode = Util.Configuration["General"]["TextEncodingCode"];
                    if (int.TryParse(iniEncodingCode, out var newEncodingCode))
                    {
                        // TODO: Check a list of supported encoding codes.
                        if (newEncodingCode is not 932 and (< 1250 or > 1252))
                        {
                            throw new Exception($"Encoding code '{newEncodingCode}' is not supported. See TES3Merge.ini for supported values.");
                        }

                        // Register the encoding provider so we can understand 1252 and presumably others.
                        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                        var encoding = Encoding.GetEncoding(newEncodingCode);
                        Util.Logger.WriteLine($"Using encoding: {encoding.EncodingName}");
                        Utility.Common.TextEncodingCode = newEncodingCode;
                    }
                    else
                    {
                        throw new Exception($"Encoding code '{iniEncodingCode}' is not a valid integer. See TES3Merge.ini for supported values.");
                    }
                }
                catch (Exception e)
                {
                    // Write the exception as a warning and set the default Windows-1252 encoding.
                    WriteToLogAndConsole($"WARNING: Could not resolve default text encoding code: {e.Message}");
                    Console.WriteLine("Default encoding of Windows-1252 (English) will be used.");
                    Utility.Common.TextEncodingCode = 1252;
                }

                // Find out where Morrowind lives.
                var morrowindPath = GetMorrowindFolder();
                if (string.IsNullOrEmpty(morrowindPath))
                {
                    throw new Exception($"ERROR: Could not resolve Morrowind directory. Install TES3Merge into Morrowind\\TES3Merge\\TES3Merge.exe or reinstall Morrowind to fix registry values.");
                }
                Logger.WriteLine($"Morrowind found at '{morrowindPath}'.");

                // Get a list of supported mergable object types.
                var supportedMergeTags = new List<string>
                {
                    "ACTI",
                    "ALCH",
                    "APPA",
                    "ARMO",
                    "BODY",
                    "BOOK",
                    "BSGN",
                    //"CELL",
                    "CLAS",
                    "CLOT",
                    "CONT",
                    "CREA",
                    //"DIAL",
                    "DOOR",
                    "ENCH",
                    //"FACT",
                    //"GLOB",
                    "GMST",
                    //"INFO",
                    "INGR",
                    //"LAND",
                    //"LEVC",
                    //"LEVI",
                    "LIGH",
                    "LOCK",
                    //"LTEX",
                    "MGEF",
                    "MISC",
                    "NPC_",
                    //"PGRD",
                    "PROB",
                    "RACE",
                    //"REFR",
                    //"REGN",
                    "REPA",
                    //"SCPT",
                    "SKIL",
                    "SNDG",
                    "SOUN",
                    "SPEL",
                    "STAT",
                    "WEAP",
                };

                // Allow INI to remove types from merge.
                foreach (var recordTypeConfig in Configuration["RecordTypes"])
                {
                    if (bool.TryParse(recordTypeConfig.Value, out var supported) && !supported)
                    {
                        supportedMergeTags.Remove(recordTypeConfig.KeyName);
                    }
                }
                WriteToLogAndConsole($"Supported record types: {string.Join(", ", supportedMergeTags)}");

                // Make sure we're going to merge something.
                if (supportedMergeTags.Count == 0)
                {
                    WriteToLogAndConsole("ERROR: No valid record types to merge. Check TES3Merge.ini's configuration.");
                    return;
                }

                // Get object ID filtering from INI.
                var objectIdFilters = new List<KeyValuePair<string, bool>>();
                foreach (var kv in Configuration["ObjectFilters"])
                {
                    if (bool.TryParse(kv.Value, out var allow))
                    {
                        objectIdFilters.Add(new KeyValuePair<string, bool>(kv.KeyName.Trim('"'), allow));
                    }
                    else
                    {
                        WriteToLogAndConsole($"WARNING: Filter {kv.KeyName} could not be parsed.");
                    }
                }

                // Get the game file list from the ini file.
                var sortedMasters = new List<string>();
                Console.WriteLine("Parsing content files...");
                {
                    // Try to get INI information.
                    IniData data;
                    try
                    {
                        var parser = new FileIniDataParser();
                        data = parser.ReadFile(Path.Combine(morrowindPath, "Morrowind.ini"));
                    }
                    catch (Exception firstTry)
                    {
                        try
                        {
                            // Try again with invalid line skipping.
                            var parser = new FileIniDataParser();
                            var config = parser.Parser.Configuration;
                            config.SkipInvalidLines = true;
                            config.AllowDuplicateKeys = true;
                            config.AllowDuplicateSections = true;
                            data = parser.ReadFile(Path.Combine(morrowindPath, "Morrowind.ini"));

                            // If the first pass fails, be more forgiving, but let the user know their INI has issues.
                            Console.WriteLine("WARNING: Issues were found with your Morrowind.ini file. See TES3Merge.log for details.");
                            Logger.WriteLine($"WARNING: Could not parse Morrowind.ini with initial pass. Error: {firstTry.Message}");
                        }
                        catch (Exception secondTry)
                        {
                            Console.WriteLine("ERROR: Unrecoverable issues were found with your Morrowind.ini file. See TES3Merge.log for details.");
                            Logger.WriteLine($"ERROR: Could not parse Morrowind.ini with second pass. Error: {secondTry.Message}");

                            ShowCompletionPrompt();
                            return;
                        }
                    }

                    // Get a list of ignored files.
                    var fileFilters = new HashSet<string>();
                    foreach (var kv in Configuration["FileFilters"])
                    {
                        if (bool.TryParse(kv.Value, out var allow) && !allow)
                        {
                            fileFilters.Add(kv.KeyName.ToLower());
                        }
                    }

                    // Build a list of activated files.
                    var activatedMasters = new HashSet<string>();
                    var gameFileIndex = 0;
                    while (true)
                    {
                        var gameFile = data["Game Files"]["GameFile" + gameFileIndex];
                        if (string.IsNullOrEmpty(gameFile))
                        {
                            break;
                        }

                        // Hard filters.
                        if (gameFile is "Merged_Objects.esp" or "Merged Objects.esp")
                        {
                            gameFileIndex++;
                            continue;
                        }

                        // Check for custom filters.
                        if (fileFilters.Contains(gameFile.ToLower()))
                        {
                            Console.WriteLine($"Ignoring file: {gameFile}");
                            Logger.WriteLine($"Ignoring file: {gameFile}");
                            gameFileIndex++;
                            continue;
                        }

                        // Add to masters list.
                        activatedMasters.Add(gameFile);
                        gameFileIndex++;
                    }

                    // Add all ESM files first, then ESP files.
                    foreach (var path in Directory.GetFiles(Path.Combine(morrowindPath, "Data Files"), "*.esm", SearchOption.TopDirectoryOnly).OrderBy(p => File.GetLastWriteTime(p).Ticks))
                    {
                        var fileName = Path.GetFileName(path);
                        if (activatedMasters.Contains(fileName))
                        {
                            sortedMasters.Add(fileName);
                        }
                    }
                    foreach (var path in Directory.GetFiles(Path.Combine(morrowindPath, "Data Files"), "*.esp", SearchOption.TopDirectoryOnly).OrderBy(p => File.GetLastWriteTime(p).Ticks))
                    {
                        var fileName = Path.GetFileName(path);
                        if (activatedMasters.Contains(fileName))
                        {
                            sortedMasters.Add(fileName);
                        }
                    }

                    // map all files in Data Files
                    // TODO suport bsas
                    WriteToLogAndConsole($"Generating file list ... ");
                    var excludedExtensions = new List<string>() { ".esp", ".ESP", ".esm", ".bsa", ".json", ".exe", "", ".html", ".txt",
                    ".css", ".lua", ".pdf", ".jpg", ".ini", ".md", ".mohidden", ".docx",
                    ".7z",".rtf", ".log", ".psd", ".ods", ".csv", ".pkl", ".bak",
                    ".luacheckrc", ".luacompleterc", ".cells", ".data", ".espf", ".esmf", ".dll"};
                    var excludedFolders = new List<string>() { "docs", "distantland", "mwse", "extras", "mash" };

                    var dataFiles = Path.Combine(morrowindPath, "Data Files");
                    var physicalFiles = Directory
                        .GetFiles(dataFiles, "*", SearchOption.AllDirectories)
                        .Select(x => x.Substring(dataFiles.Length + 1))
                        .Where(x => !excludedExtensions.Contains(Path.GetExtension(x)))
                        .Select(x => x.ToLower());


                    // get bsas
                    var bsaFiles = Directory.GetFiles(dataFiles, "*.bsa", SearchOption.TopDirectoryOnly);
                    foreach (var bsaFile in bsaFiles)
                    {
                        using var fs = new FileStream(bsaFile, FileMode.Open);
                        var bsa = BsaParser.Read(fs);
                        if (bsa is not null)
                        {
                            var bsaAssets = bsa.Files.Select(x => x.Name);
                            physicalFiles = physicalFiles.Union(bsaAssets);
                        }

                    }


                    var fileMap = physicalFiles.ToLookup(p => Path.GetExtension(p), p => p);

                    extensionToFolderMap = new Dictionary<string, List<string>>();
                    foreach (var grouping in fileMap)
                    {
                        var key = grouping.Key;
                        var group = fileMap[key];

                        var list = new List<string>();
                        foreach (var item in group)
                        {
                            var splits = item.Split(Path.DirectorySeparatorChar);
                            var first = splits.FirstOrDefault();
                            if (string.IsNullOrEmpty(first))
                            {
                                continue;
                            }
                            if (splits.Length == 1)
                            {
                                continue;
                            }
                            if (excludedFolders.Contains(first))
                            {
                                continue;
                            }
                            if (Path.HasExtension(first))
                            {
                                continue;
                            }

                            if (!list.Contains(first))
                            {
                                list.Add(first);
                            }
                        }

                        if (list.Count > 0)
                        {
                            extensionToFolderMap.Add(key, list);
                        }
                    }

#if DEBUG
                    var folderJson = JsonSerializer.Serialize(extensionToFolderMap, new JsonSerializerOptions() { WriteIndented = true });
                    WriteToLogAndConsole(folderJson);
                    var dbgPath = Path.Combine(morrowindPath, "Data Files", "__dbg.json");
                    File.WriteAllText(dbgPath, folderJson);
#endif

                    var reportDict = new Dictionary<string, Dictionary<string, List<string>>>();

                    // Go through and build a record list.
                    WriteToLogAndConsole($"Parsing plugins ... ");
                    foreach (var sortedMaster in sortedMasters)
                    {
                        if (Path.GetExtension(sortedMaster) == ".esm")
                        {
                            continue;
                        }

                        var map = new Dictionary<string, List<string>>();

                        // go through all records
                        WriteToLogAndConsole($"Parsing input file: {sortedMaster}");
                        var fullGameFilePath = Path.Combine(morrowindPath, "Data Files", $"{sortedMaster}");
                        var file = TES3.TES3Load(fullGameFilePath, supportedMergeTags);
                        foreach (var record in file.Records)
                        {
                            #region checks

                            if (record is null)
                            {
                                continue;
                            }
                            if (record.GetType().Equals(typeof(TES3Lib.Records.TES3)))
                            {
                                continue;
                            }
                            var editorId = record.GetEditorId().Replace("\0", string.Empty);
                            if (string.IsNullOrEmpty(editorId))
                            {
                                continue;
                            }

                            // Check against object filters.
                            var allow = true;
                            var lowerId = editorId.ToLower();
                            foreach (var kv in objectIdFilters)
                            {
                                try
                                {
                                    if (Regex.Match(lowerId, kv.Key).Success)
                                    {
                                        allow = kv.Value;
                                    }
                                }
                                catch (Exception)
                                {

                                }
                            }
                            if (!allow)
                            {
                                continue;
                            }

                            #endregion

                            // verify here
                            GetPathsInRecord(record, map, fileMap);
                        }

                        if (map.Count > 0)
                        {
                            reportDict.Add(sortedMaster, map);
                        }
                    }

                    // pretty print
                    WriteToLogAndConsole($"\n------------------------------------");
                    WriteToLogAndConsole($"Results:\n");
                    foreach (var (plugin, val) in reportDict)
                    {
                        WriteToLogAndConsole($"\n{plugin} ({val.Count})");
                        foreach (var (recordID, list) in val)
                        {
                            foreach (var item in list)
                            {
                                //Console.WriteLine("{0,-20} {1,5}\n", "Name", "Hours");
                                WriteToLogAndConsole(string.Format("\t{0,-40} {1,5}", recordID, item));
                            }
                        }
                    }
                    // serialize
                    WriteToLogAndConsole($"\n");
                    var reportPath = Path.Combine(morrowindPath, "Data Files", "report.json");
                    WriteToLogAndConsole($"Writing report to: {reportPath}");
                    {
                        using var fs = new FileStream(reportPath, FileMode.Create);
                        JsonSerializer.Serialize(fs, reportDict, new JsonSerializerOptions() { WriteIndented = true });
                    }
                }

                ShowCompletionPrompt();
            }
#if DEBUG == false
        catch (Exception e)
        {
            Console.WriteLine("A serious error has occurred. Please post the TES3Merge.log file to GitHub: https://github.com/NullCascade/TES3Merge/issues");
            Logger.WriteLine("An unhandled exception has occurred. Traceback:");
            Logger.WriteLine(e.Message);
            Logger.WriteLine(e.StackTrace);
            ShowCompletionPrompt();
        }
#endif

            /// <summary>
            /// loop through all subrecords of a record 
            /// </summary>
            /// <param name="record"></param>
            /// <param name="map"></param>
            /// <param name="fileMap"></param>
            static void GetPathsInRecord(
                Record record,
                Dictionary<string, List<string>> map,
                ILookup<string, string> fileMap)
            {
                var recordDict = new List<string>();
                var properties = record
                                    .GetType()
                                    .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                    .OrderBy(x => x.MetadataToken)
                                    .ToList();

                foreach (var property in properties)
                {
                    var val = record is not null ? property.GetValue(record) : null;
                    if (val is Subrecord subrecord)
                    {
                        GetPathsInSubRecordRecursive(subrecord, recordDict, fileMap);
                    }
                }

                if (recordDict.Count > 0)
                {
                    var id = record.GetEditorId().TrimEnd('\0');
                    map.Add(id, recordDict);
                }
            }
        }

        /// <summary>
        /// Loop through all properties of a subrecord
        /// and check if a property is a file path
        /// then checks if that file exists in the filemap
        /// </summary>
        /// <param name="subRecord"></param>
        /// <param name="map"></param>
        /// <param name="fileMap"></param>
        private static void GetPathsInSubRecordRecursive(
            Subrecord subRecord,
            List<string> map,
            ILookup<string, string> fileMap)
        {
            ArgumentNullException.ThrowIfNull(extensionToFolderMap);

            var recordTypeName = subRecord.Name;
            var properties = subRecord
                                .GetType()
                                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                .OrderBy(x => x.MetadataToken)
                                .ToList()!;

            foreach (var property in properties)
            {
                var val = subRecord is not null ? property.GetValue(subRecord) : null;
                if (val is string rawstr)
                {
                    var str = rawstr.TrimEnd('\0').ToLower();
                    var extension = Path.GetExtension(str);

                    if (string.IsNullOrEmpty(extension) || !extensionToFolderMap.ContainsKey(extension))
                    {
                        continue;
                    }

                    // formatting
                    if (str.Contains(Path.AltDirectorySeparatorChar))
                    {
                        str = str.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                    }
                    var filesOfExtension = fileMap[extension];
                    var found = false;

                    // check all folders 
                    var possibleFolders = extensionToFolderMap[extension];
                    foreach (var folder in possibleFolders)
                    {
                        var resolved = $"{folder}\\{str}";
                        if (filesOfExtension.Contains(resolved))
                        {
                            found = true;
                            break;
                        }
                    }

                    // check both tga and dds because they can be used interchangeably for some reason
                    if (!found && extension == ".tga")
                    {
                        filesOfExtension = fileMap[".dds"];
                        foreach (var folder in extensionToFolderMap[".dds"])
                        {
                            var resolved = $"{folder}\\{str}";
                            resolved = Path.ChangeExtension(resolved, ".dds");
                            if (filesOfExtension.Contains(resolved))
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        // resolve possible file paths
                        var resolvedPath = possibleFolders.Count > 1
                            ? Path.Combine($"<{string.Join(',', possibleFolders)}>", str)
                            : Path.Combine($"{possibleFolders.First()}", str);
                        map.Add(resolvedPath);
                    }
                }
                else
                {
                    if (val is Subrecord subrecord)
                    {
                        GetPathsInSubRecordRecursive(subrecord, map, fileMap);
                    }
                }
            }

        }


    }
}
