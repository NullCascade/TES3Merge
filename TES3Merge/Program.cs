using IniParser;
using IniParser.Model;
using Microsoft.Win32;
using System.CommandLine;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using TES3Lib;

namespace TES3Merge;

class Program
{
    public static StreamWriter Logger = new("TES3Merge.log", false)
    {
        AutoFlush = true
    };

    public static IniData? Configuration;

    /// <summary>
    /// Finds the relevant Morrowind directory. It will prefer a directory that is shares or is parent to the current folder.
    /// </summary>
    /// <returns>A path to the directory where Morrowind.exe resides, or null if it could not be determined.</returns>
    static string? GetMorrowindFolder()
    {
        if (File.Exists("Morrowind.exe"))
        {
            return Directory.GetCurrentDirectory();
        }
        else if (File.Exists(Path.Combine("..", "Morrowind.exe")))
        {
            return Directory.GetParent(Directory.GetCurrentDirectory())?.FullName;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var registryValue = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\bethesda softworks\\Morrowind", "Installed Path", null) as string;
            if (!string.IsNullOrEmpty(registryValue) && File.Exists(Path.Combine(registryValue, "Morrowind.exe")))
            {
                return registryValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns a list that is a copy of the load order, filtered to certain results.
    /// </summary>
    /// <param name="loadOrder">The base sorted load order collection.</param>
    /// <param name="filter">The filter to include elements from.</param>
    /// <returns>A copy of <paramref name="loadOrder"/>, filtered to only elements that match with <paramref name="filter"/>.</returns>
    static List<string> GetFilteredLoadList(List<string> loadOrder, IEnumerable<string> filter)
    {
        var result = new List<string>();

        foreach (var file in loadOrder)
        {
            if (filter.Contains(file))
            {
                result.Add(file);
            }
        }

        return result;
    }

    /// <summary>
    /// Writes to both the console and the log file.
    /// </summary>
    /// <param name="Message">Message to write.</param>
    public static void WriteToLogAndConsole(string Message)
    {
        Logger.WriteLine(Message);
        Console.WriteLine(Message);
    }

    // main entry point to parse commandline options
    static async Task Main(string[] args)
    {
        var option = new Option<bool>(new[] { "--inclusive-list", "-i" }, "Merge lists inclusively per element (implemented for List<NPCO>)");
        var rootCommand = new RootCommand
        {
            option
        };

        rootCommand.SetHandler((bool inclusiveListMerge) => { Run(inclusiveListMerge); }, option);
        await rootCommand.InvokeAsync(args);
    }


    // main command to run
    static void Run(bool inclusiveListMerge)
    {
#if DEBUG
        //Console.WriteLine("Press any key to continue...");
        //Console.ReadKey();
#endif

        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        Logger.WriteLine($"TES3Merge v{version}.");

        // Main execution attempt.
#if DEBUG == false
        try
#endif
        {
            // Load this application's configuration.
            {
                var parser = new FileIniDataParser();
                var iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TES3Merge.ini");
                Configuration = parser.ReadFile(iniPath);
            }

            // Determine what encoding to use.
            try
            {
                var iniEncodingCode = Configuration["General"]["TextEncodingCode"];
                if (int.TryParse(iniEncodingCode, out var newEncodingCode))
                {
                    // TODO: Check a list of supported encoding codes.
                    if (newEncodingCode != 932 && (newEncodingCode < 1250 || newEncodingCode > 1252))
                    {
                        throw new Exception($"Encoding code '{newEncodingCode}' is not supported. See TES3Merge.ini for supported values.");
                    }

                    // Register the encoding provider so we can understand 1252 and presumably others.
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                    var encoding = Encoding.GetEncoding(newEncodingCode);
                    Logger.WriteLine($"Using encoding: {encoding.EncodingName}");
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

            // Create our merged object TES3 file.
            var mergedObjects = new TES3();
            var mergedObjectsHeader = new TES3Lib.Records.TES3
            {
                HEDR = new TES3Lib.Subrecords.TES3.HEDR()
                {
                    CompanyName = "TES3Merge",
                    Description = $"Automatic merge generated at {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}.",
                    Version = 1.3f,
                }
            };
            mergedObjects.Records.Add(mergedObjectsHeader);

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
            foreach (KeyData? recordTypeConfig in Configuration["RecordTypes"])
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
            foreach (KeyData? kv in Configuration["ObjectFilters"])
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

            // Collections for managing our objects.
            var recordOverwriteMap = new Dictionary<string, Dictionary<string, List<TES3Lib.Base.Record>>>();

            // Get the game file list from the ini file.
            var sortedMasters = new List<string>();
            var mapTES3ToFileNames = new Dictionary<TES3, string>();
            var recordMasters = new Dictionary<TES3Lib.Base.Record, TES3>();
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
                        IniParser.Model.Configuration.IniParserConfiguration? config = parser.Parser.Configuration;
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
                foreach (KeyData? kv in Configuration["FileFilters"])
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
                    if (gameFile == "Merged_Objects.esp" || gameFile == "Merged Objects.esp")
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

                // Go through and build a record list.
                foreach (var sortedMaster in sortedMasters)
                {
                    var fullGameFilePath = Path.Combine(morrowindPath, "Data Files", $"{sortedMaster}");
                    DateTime lastWriteTime = File.GetLastWriteTime(fullGameFilePath);
                    Logger.WriteLine($"Parsing input file: {sortedMaster} @ {lastWriteTime}");
                    var file = TES3.TES3Load(fullGameFilePath, supportedMergeTags);
                    mapTES3ToFileNames[file] = sortedMaster;

                    foreach (TES3Lib.Base.Record? record in file.Records)
                    {
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
                        foreach (KeyValuePair<string, bool> kv in objectIdFilters)
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

                        if (!recordOverwriteMap.ContainsKey(record.Name))
                        {
                            recordOverwriteMap[record.Name] = new Dictionary<string, List<TES3Lib.Base.Record>>();
                        }

                        Dictionary<string, List<TES3Lib.Base.Record>>? map = recordOverwriteMap[record.Name];
                        if (!map.ContainsKey(editorId))
                        {
                            map[editorId] = new List<TES3Lib.Base.Record>();
                        }

                        map[editorId].Add(record);
                        recordMasters[record] = file;
                    }
                }
            }

            // Check to see if we have any potential merges.
            if (recordMasters.Count == 0)
            {
                WriteToLogAndConsole("No potential record merges found. Aborting.");
                ShowCompletionPrompt();
                return;
            }

            // commandline arguments
            if (inclusiveListMerge)
            {
                RecordMerger.MergePropertyFunctionMapper[typeof(List<TES3Lib.Subrecords.Shared.NPCO>)] = Merger.Shared.ItemsList;
            }

            // Go through and build merged objects.
            if (!bool.TryParse(Configuration["General"]["DumpMergedRecordsToLog"], out var dumpMergedRecordsToLog))
            {
                dumpMergedRecordsToLog = false;
            }
            Console.WriteLine("Building merges...");
            var usedMasters = new HashSet<string>();
            foreach (var recordType in recordOverwriteMap.Keys)
            {
                Dictionary<string, List<TES3Lib.Base.Record>>? recordsMap = recordOverwriteMap[recordType];
                foreach (var id in recordsMap.Keys)
                {
                    List<TES3Lib.Base.Record>? records = recordsMap[id];
                    if (records.Count > 2)
                    {
                        TES3Lib.Base.Record? firstRecord = records[0];
                        TES3Lib.Base.Record? lastRecord = records.Last();
                        var firstMaster = mapTES3ToFileNames[recordMasters[firstRecord]];
                        var lastMaster = mapTES3ToFileNames[recordMasters[lastRecord]];

                        var localUsedMasters = new HashSet<string>() { firstMaster, lastMaster };

                        var lastSerialized = lastRecord.GetRawLoadedBytes();
                        TES3Lib.Base.Record newRecord = Activator.CreateInstance(lastRecord.GetType(), new object[] { lastSerialized }) as TES3Lib.Base.Record ?? throw new Exception("Could not create activator instance.");
                        for (var i = records.Count - 2; i > 0; i--)
                        {
                            TES3Lib.Base.Record? record = records[i];
                            var master = mapTES3ToFileNames[recordMasters[record]];
                            try
                            {
                                if (RecordMerger.Merge(newRecord, firstRecord, record))
                                {
                                    localUsedMasters.Add(master);
                                }
                            }
                            catch (Exception e)
                            {

                                List<string>? masterListArray = GetFilteredLoadList(sortedMasters, localUsedMasters);
                                masterListArray.Add(master);
                                var masterList = string.Join(", ", masterListArray);
                                WriteToLogAndConsole($"Failed to merge {firstRecord.Name} record '{id}' from mods: {masterList}");
                                foreach (TES3Lib.Base.Record? r in records)
                                {
                                    WriteToLogAndConsole($">> {mapTES3ToFileNames[recordMasters[r]]}: {BitConverter.ToString(r.GetRawLoadedBytes()).Replace("-", "")}");
                                }

                                WriteToLogAndConsole(e.Message);

                                if (e.StackTrace is not null)
                                {
                                    WriteToLogAndConsole(e.StackTrace);
                                }

                                ShowCompletionPrompt();
                                return;
                            }
                        }

                        try
                        {
                            var newSerialized = newRecord.SerializeRecord();
                            if (!lastSerialized.SequenceEqual(newSerialized))
                            {
                                Console.WriteLine($"Merged {newRecord.Name} record: {id}");
                                mergedObjects.Records.Add(newRecord);

                                var masterList = string.Join(", ", GetFilteredLoadList(sortedMasters, localUsedMasters).ToArray());
                                Logger.WriteLine($"Resolved conflicts for {firstRecord.Name} record '{id}' from mods: {masterList}");

                                if (dumpMergedRecordsToLog)
                                {
                                    foreach (TES3Lib.Base.Record? record in records)
                                    {
                                        var master = mapTES3ToFileNames[recordMasters[record]];
                                        Logger.WriteLine($">> {master}: {BitConverter.ToString(record.GetRawLoadedBytes()).Replace("-", "")}");
                                    }
                                    Logger.WriteLine($">> Merged Objects.esp: {BitConverter.ToString(newSerialized).Replace("-", "")}");
                                }

                                foreach (var master in localUsedMasters)
                                {
                                    usedMasters.Add(master);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            var masterList = string.Join(", ", GetFilteredLoadList(sortedMasters, localUsedMasters).ToArray());
                            WriteToLogAndConsole($"Could not resolve conflicts for {firstRecord.Name} record '{id}' from mods: {masterList}");
                            foreach (TES3Lib.Base.Record? record in records)
                            {
                                var master = mapTES3ToFileNames[recordMasters[record]];
                                WriteToLogAndConsole($">> {master}: {BitConverter.ToString(record.GetRawLoadedBytes()).Replace("-", "")}");
                            }

                            WriteToLogAndConsole(e.Message);

                            if (e.StackTrace is not null)
                            {
                                WriteToLogAndConsole(e.StackTrace);
                            }

                            ShowCompletionPrompt();
                            return;
                        }
                    }
                }
            }

            // Did we even merge anything?
            if (usedMasters.Count == 0)
            {
                WriteToLogAndConsole("No merges were deemed necessary. Aborting.");
                ShowCompletionPrompt();
                return;
            }

            // Add the necessary masters.
            Logger.WriteLine("Saving Merged Objects.esp ...");
            mergedObjectsHeader.Masters = new List<(TES3Lib.Subrecords.TES3.MAST MAST, TES3Lib.Subrecords.TES3.DATA DATA)>();
            foreach (var gameFile in GetFilteredLoadList(sortedMasters, usedMasters))
            {
                if (usedMasters.Contains(gameFile))
                {
                    var size = new FileInfo(Path.Combine(morrowindPath, "Data Files", $"{gameFile}")).Length;
                    mergedObjectsHeader.Masters.Add((new TES3Lib.Subrecords.TES3.MAST { Filename = $"{gameFile}\0" }, new TES3Lib.Subrecords.TES3.DATA { MasterDataSize = size }));
                }
            }

            // Save out the merged objects file.
            mergedObjectsHeader.HEDR.NumRecords = mergedObjects.Records.Count - 1;
            mergedObjects.TES3Save(Path.Combine(morrowindPath, "Data Files", "Merged Objects.esp"));
            Logger.WriteLine($"Wrote {mergedObjects.Records.Count - 1} merged objects.");

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
    }

    private static void ShowCompletionPrompt()
    {
        if (Configuration is not null && bool.TryParse(Configuration["General"]["PauseOnCompletion"], out var pauseOnCompletion) && pauseOnCompletion)
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
