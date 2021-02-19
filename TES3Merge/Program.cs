using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using IniParser;
using IniParser.Model;
using Microsoft.Win32;
using TES3Lib;

namespace TES3Merge
{
    class Program
    {
        public static StreamWriter Logger;
        public static IniData Configuration;

        /// <summary>
        /// Finds the relevant Morrowind directory. It will prefer a directory that is shares or is parent to the current folder.
        /// </summary>
        /// <returns>A path to the directory where Morrowind.exe resides, or null if it could not be determined.</returns>
        static string GetMorrowindFolder()
        {
            if (File.Exists("Morrowind.exe"))
            {
                return Directory.GetCurrentDirectory();
            }
            else if (File.Exists(Path.Combine("..", "Morrowind.exe")))
            {
                return Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
            }
            else
            {
                string registryValue = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\bethesda softworks\\Morrowind", "Installed Path", null) as String;
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
            List<string> result = new List<string>();

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

        static void Main(string[] args)
        {
#if DEBUG
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
#endif

            // Create our log.
            Logger = new StreamWriter("TES3Merge.log", false)
            {
                AutoFlush = true
            };
            
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Logger.WriteLine($"TES3Merge v0.7.");

            // Main execution attempt.
#if DEBUG == false
            try
#endif
            {
                // Load this application's configuration.
                {
                    var parser = new FileIniDataParser();
                    string iniPath = Path.Combine($"{AppDomain.CurrentDomain.BaseDirectory}", "TES3Merge.ini");
                    Configuration = parser.ReadFile(iniPath);
                }

                // Determine what encoding to use.
                try
                {
                    var iniEncodingCode = Configuration["General"]["TextEncodingCode"];
                    if (int.TryParse(iniEncodingCode, out int newEncodingCode))
                    {
                        // TODO: Check a list of supported encoding codes.
                        if (newEncodingCode != 932 && (newEncodingCode < 1250 || newEncodingCode > 1252))
                        {
                            throw new Exception($"Encoding code '{newEncodingCode}' is not supported. See TES3Merge.ini for supported values.");
                        }

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
                string morrowindPath = GetMorrowindFolder();
                if (morrowindPath == null)
                {
                    WriteToLogAndConsole("ERROR: Could not resolve Morrowind directory. Install TES3Merge folder into the Morrowind installation folder.");
                }
                Logger.WriteLine($"Morrowind found at '{morrowindPath}'.");

                // Create our merged object TES3 file.
                TES3 mergedObjects = new TES3();
                var mergedObjectsHeader = new TES3Lib.Records.TES3
                {
                    HEDR = new TES3Lib.Subrecords.TES3.HEDR()
                    {
                        CompanyName = "TES3Merge",
                        Description = $"Automatic merge generated at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}.",
                        Version = 1.3f,
                    }
                };
                mergedObjects.Records.Add(mergedObjectsHeader);

                // Get a list of supported mergable object types.
                List<string> supportedMergeTags = new List<string>
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
                    bool.TryParse(recordTypeConfig.Value, out bool supported);
                    if (!supported)
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
                List<KeyValuePair<string, bool>> objectIdFilters = new List<KeyValuePair<string, bool>>();
                foreach (var kv in Configuration["ObjectFilters"])
                {
                    bool.TryParse(kv.Value, out bool allow);
                    objectIdFilters.Add(new KeyValuePair<string, bool>(kv.KeyName.Trim('"'), allow));
                }

                // Collections for managing our objects.
                Dictionary<string, Dictionary<string, List<TES3Lib.Base.Record>>> recordOverwriteMap = new Dictionary<string, Dictionary<string, List<TES3Lib.Base.Record>>>();

                // Get the game file list from the ini file.
                List<string> sortedMasters = new List<string>();
                Dictionary<TES3, string> mapTES3ToFileNames = new Dictionary<TES3, string>();
                Dictionary<TES3Lib.Base.Record, TES3> recordMasters = new Dictionary<TES3Lib.Base.Record, TES3>();
                Console.WriteLine("Parsing content files...");
                {
                    // Try to get INI information.
                    IniData data;
                    try
                    {
                        var parser = new FileIniDataParser();
                        data = parser.ReadFile(Path.Combine($"{morrowindPath}", "Morrowind.ini"));
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
                            data = parser.ReadFile(Path.Combine($"{morrowindPath}", "Morrowind.ini"));

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
                    HashSet<string> fileFilters = new HashSet<string>();
                    foreach (var kv in Configuration["FileFilters"])
                    {
                        bool.TryParse(kv.Value, out bool allow);
                        if (!allow)
                        {
                            fileFilters.Add(kv.KeyName.ToLower());
                        }
                    }

                    // Build a list of activated files.
                    HashSet<string> activatedMasters = new HashSet<string>();
                    int gameFileIndex = 0;
                    while (true)
                    {
                        string gameFile = data["Game Files"]["GameFile" + gameFileIndex];
                        if (String.IsNullOrEmpty(gameFile))
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
                    foreach (var path in Directory.GetFiles(Path.Combine($"{morrowindPath}", "Data Files"), "*.esm", SearchOption.TopDirectoryOnly).OrderBy(p => File.GetLastWriteTime(p).Ticks))
                    {
                        var fileName = Path.GetFileName(path);
                        if (activatedMasters.Contains(fileName))
                        {
                            sortedMasters.Add(fileName);
                        }
                    }
                    foreach (var path in Directory.GetFiles(Path.Combine($"{morrowindPath}", "Data Files"), "*.esp", SearchOption.TopDirectoryOnly).OrderBy(p => File.GetLastWriteTime(p).Ticks))
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
                        string fullGameFilePath = Path.Combine($"{morrowindPath}", "Data Files", $"{sortedMaster}");
                        var lastWriteTime = File.GetLastWriteTime(fullGameFilePath);
                        Logger.WriteLine($"Parsing input file: {sortedMaster} @ {lastWriteTime}");
                        TES3 file = TES3.TES3Load(fullGameFilePath, supportedMergeTags);
                        mapTES3ToFileNames[file] = sortedMaster;

                        foreach (var record in file.Records)
                        {
                            if (record == null)
                            {
                                continue;
                            }

                            if (record.GetType().Equals(typeof(TES3Lib.Records.TES3)))
                            {
                                continue;
                            }

                            string editorId = record.GetEditorId().Replace("\0", string.Empty);
                            if (string.IsNullOrEmpty(editorId))
                            {
                                continue;
                            }

                            // Check against object filters.
                            bool allow = true;
                            string lowerId = editorId.ToLower();
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

                            if (!recordOverwriteMap.ContainsKey(record.Name))
                            {
                                recordOverwriteMap[record.Name] = new Dictionary<string, List<TES3Lib.Base.Record>>();
                            }

                            var map = recordOverwriteMap[record.Name];
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

                // Go through and build merged objects.
                bool.TryParse(Configuration["General"]["DumpMergedRecordsToLog"], out bool dumpMergedRecordsToLog);
                Console.WriteLine("Building merges...");
                HashSet<string> usedMasters = new HashSet<string>();
                foreach (var recordType in recordOverwriteMap.Keys)
                {
                    var recordsMap = recordOverwriteMap[recordType];
                    foreach (string id in recordsMap.Keys)
                    {
                        var records = recordsMap[id];
                        if (records.Count > 2)
                        {
                            var firstRecord = records[0];
                            var lastRecord = records.Last();
                            var firstMaster = mapTES3ToFileNames[recordMasters[firstRecord]];
                            var lastMaster = mapTES3ToFileNames[recordMasters[lastRecord]];

                            HashSet<string> localUsedMasters = new HashSet<string>() { firstMaster, lastMaster };

                            var lastSerialized = lastRecord.GetRawLoadedBytes();
                            TES3Lib.Base.Record newRecord = Activator.CreateInstance(lastRecord.GetType(), new object[] { lastSerialized }) as TES3Lib.Base.Record;
                            for (int i = records.Count - 2; i > 0; i--)
                            {
                                var record = records[i];
                                var master = mapTES3ToFileNames[recordMasters[record]];
                                if (newRecord.MergeWith(record, firstRecord))
                                {
                                    localUsedMasters.Add(master);
                                }
                            }

                            var newSerialized = newRecord.SerializeRecord();
                            if (!lastSerialized.SequenceEqual(newSerialized))
                            {
                                Console.WriteLine($"Merged {newRecord.Name} record: {id}");
                                mergedObjects.Records.Add(newRecord);

                                foreach (string master in localUsedMasters)
                                {
                                    usedMasters.Add(master);
                                }

                                string masterList = string.Join(", ", GetFilteredLoadList(sortedMasters, localUsedMasters).ToArray());
                                Logger.WriteLine($"Resolved conflicts for {firstRecord.Name} record '{id}' from mods: {masterList}");
                                
                                if (dumpMergedRecordsToLog)
                                {
                                    foreach (var record in records)
                                    {
                                        var master = mapTES3ToFileNames[recordMasters[record]];
                                        Logger.WriteLine($">> {master}: {BitConverter.ToString(record.GetRawLoadedBytes()).Replace("-", "")}");
                                    }
                                    Logger.WriteLine($">> Merged Objects.esp: {BitConverter.ToString(newSerialized).Replace("-", "")}");
                                }
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
                        long size = new FileInfo(Path.Combine($"{morrowindPath}", "Data Files", $"{gameFile}")).Length;
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
            bool.TryParse(Configuration["General"]["PauseOnCompletion"], out bool pauseOnCompletion);
            if (pauseOnCompletion)
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}
