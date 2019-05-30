using System;
using System.Collections.Generic;
using System.Deployment;
using System.IO;
using System.Linq;
using System.Reflection;

using IniParser;
using IniParser.Model;
using Microsoft.Win32;
using TES3Lib;

namespace TES3Merge
{
    class Program
    {
        static StreamWriter Logger;
        static IniData Configuration;

        static bool ListsMatch(List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)> a, List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
#pragma warning disable IDE0042 // Deconstruct variable declaration
                var first = a[i];
                var second = b[i];
#pragma warning restore IDE0042 // Deconstruct variable declaration
                if (first.INDX.Type != second.INDX.Type)
                {
                    return false;
                }

                if (first.BNAM?.EditorId != second.BNAM?.EditorId)
                {
                    return false;
                }

                if (first.CNAM?.FemalePartName != second.CNAM?.FemalePartName)
                {
                    return false;
                }
            }

            return true;
        }

        static bool MergeProperty(PropertyInfo property, object obj, object first, object next)
        {
            var currentValue = obj != null ? property.GetValue(obj) : null;
            var firstValue = first != null ? property.GetValue(first) : null;
            var nextValue = next != null ? property.GetValue(next) : null;

            // Handle null cases.
            if (currentValue == null && firstValue != null)
            {
                return false;
            }
            else if (currentValue == null && nextValue == null)
            {
                return false;
            }
            else if (firstValue != null && nextValue == null)
            {
                property.SetValue(obj, null);
                return true;
            }

            // Special handling for structures that we want to do custom merging for.
            if (property.PropertyType.Equals(typeof(List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)>)))
            {
                var currentAsList = currentValue as List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)>;
                var firstAsList = firstValue as List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)>;
                var nextAsList = nextValue as List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)>;
                if (ListsMatch(currentAsList, firstAsList) && !ListsMatch(nextAsList, firstAsList))
                {
                    property.SetValue(obj, nextAsList);
                    return true;
                }
            }
            // General case, just uses equality checks.
            else
            {
                if (currentValue.Equals(firstValue) && !nextValue.Equals(firstValue))
                {
                    property.SetValue(obj, nextValue);
                    return true;
                }
            }

            return false;
        }

        static bool MergeSubrecord(TES3Lib.Base.Subrecord subrecord, TES3Lib.Base.Subrecord first, TES3Lib.Base.Subrecord next)
        {
            if (first == next)
            {
                return false;
            }

            var properties = next.GetType()
                .GetProperties(BindingFlags.Public |
                               BindingFlags.Instance |
                               BindingFlags.DeclaredOnly)
                               .OrderBy(x => x.MetadataToken)
                               .ToList();

            bool modified = false;
            foreach (PropertyInfo property in properties)
            {
                if (MergeProperty(property, subrecord, first, next))
                {
                    modified = true;
                }
            }

            return modified;
        }

        static bool MergeRecord(TES3Lib.Base.Record record, TES3Lib.Base.Record first, TES3Lib.Base.Record next)
        {
            if (first == next)
            {
                return false;
            }

            bool modified = false;
            if (record.Flags.SequenceEqual(first.Flags) && !next.Flags.SequenceEqual(first.Flags))
            {
                record.Flags = next.Flags;
                modified = true;
            }

            var properties = next.GetType()
                .GetProperties(BindingFlags.Public |
                               BindingFlags.Instance |
                               BindingFlags.DeclaredOnly)
                               .OrderBy(x => x.MetadataToken)
                               .ToList();

            foreach (PropertyInfo property in properties)
            {
                if (property.PropertyType.IsSubclassOf(typeof(TES3Lib.Base.Subrecord)))
                {
                    var currentValue = property.GetValue(record) as TES3Lib.Base.Subrecord;
                    var firstValue = property.GetValue(first) as TES3Lib.Base.Subrecord;
                    var nextValue = property.GetValue(next) as TES3Lib.Base.Subrecord;

                    // Handle null cases.
                    if (currentValue == null && firstValue == null)
                    {
                        continue;
                    }
                    else if (firstValue != null && nextValue == null)
                    {
                        property.SetValue(record, null);
                        modified = true;
                    }
                    else if (MergeSubrecord(currentValue, firstValue, nextValue))
                    {
                        modified = true;
                    }
                }
                else
                {
                    if (MergeProperty(property, record, first, next))
                    {
                        modified = true;
                    }
                }
            }

            return modified;
        }

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
            else if (File.Exists("..\\Morrowind.exe"))
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

        static SortedList<int, string> GetFilteredLoadList(SortedList<int, string> loadOrder, IEnumerable<string> filter)
        {
            SortedList<int, string> result = new SortedList<int, string>();

            foreach (var pair in loadOrder)
            {
                if (filter.Contains(pair.Value))
                {
                    result[pair.Key] = pair.Value;
                }
            }

            return result;
        }

        static void WriteToLogAndConsole(string Message)
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

            var x = System.Reflection.Assembly.GetExecutingAssembly();
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Logger.WriteLine($"TES3Merge v0.2.");

            // Main execution attempt.
#if DEBUG == false
            try
#endif
            {
                // Load this application's configuration.
                {
                    var parser = new FileIniDataParser();
                    string iniPath = $"{AppDomain.CurrentDomain.BaseDirectory}\\TES3Merge.ini";
                    Configuration = parser.ReadFile(iniPath);
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
                    "ACTI", "ALCH", "APPA", "ARMO", "BOOK", "CLOT", "CONT", "CREA", "DOOR", "ENCH",
                    "INGR", "LIGH", "LOCK", "GMST", "MISC", "NPC_", "PROB", "RACE", "REPA", "WEAP"
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
                Logger.WriteLine($"Supported record types: {string.Join(", ", supportedMergeTags)}");

                // Collections for managing our objects.
                Dictionary<string, Dictionary<string, List<TES3Lib.Base.Record>>> recordOverwriteMap = new Dictionary<string, Dictionary<string, List<TES3Lib.Base.Record>>>();

                // Get the game file list from the ini file.
                SortedList<int, string> sortedMasters = new SortedList<int, string>();
                Dictionary<TES3, string> masterFileNames = new Dictionary<TES3, string>();
                Dictionary<TES3Lib.Base.Record, TES3> recordMasters = new Dictionary<TES3Lib.Base.Record, TES3>();
                {
                    // Try to get INI information.
                    IniData data;
                    var parser = new FileIniDataParser();
                    try
                    {
                        data = parser.ReadFile($"{morrowindPath}\\Morrowind.ini");
                    }
                    catch (Exception e)
                    {
                        // If the first pass fails, be more forgiving, but let the user know their INI has issues.
                        Console.WriteLine("WARNING: Issues were found with your Morrowind.ini file. See TES3Merge.log for details.");
                        Logger.WriteLine($"WARNING: Could not parse INI with initial pass. Error: {e.Message}");

                        // Try again with invalid line skipping.
                        var config = parser.Parser.Configuration;
                        config.SkipInvalidLines = true;
                        data = parser.ReadFile($"{morrowindPath}\\Morrowind.ini");
                    }

                    // Build a list of valid files.
                    for (int i = 0; i < 255; i++)
                    {
                        string gameFile = data["Game Files"]["GameFile" + i];
                        if (gameFile == null)
                        {
                            break;
                        }

                        if (gameFile == "Merged Objects.esp")
                        {
                            continue;
                        }

                        Logger.WriteLine($"Parsing input file: {gameFile}");
                        TES3 file = TES3.TES3Load(morrowindPath + "\\Data Files\\" + gameFile, supportedMergeTags);
                        masterFileNames[file] = gameFile;
                        sortedMasters[i] = gameFile;

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

                            if (!recordOverwriteMap.ContainsKey(record.Name))
                            {
                                recordOverwriteMap[record.Name] = new Dictionary<string, List<TES3Lib.Base.Record>>();
                            }

                            string editorId = record.GetEditorId().Replace("\0", string.Empty);
                            if (string.IsNullOrEmpty(editorId))
                            {
                                continue;
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
                            var firstMaster = masterFileNames[recordMasters[firstRecord]];
                            var lastMaster = masterFileNames[recordMasters[lastRecord]];

                            HashSet<string> localUsedMasters = new HashSet<string>() { firstMaster, lastMaster };

                            TES3Lib.Base.Record newRecord = Activator.CreateInstance(lastRecord.GetType(), new object[] { lastRecord.SerializeRecord() }) as TES3Lib.Base.Record;
                            for (int i = records.Count - 2; i > 0; i--)
                            {
                                var record = records[i];
                                if (MergeRecord(newRecord, firstRecord, record))
                                {
                                    localUsedMasters.Add(masterFileNames[recordMasters[record]]);
                                }
                            }

                            if (!lastRecord.SerializeRecord().SequenceEqual(newRecord.SerializeRecord()))
                            {
                                Console.WriteLine($"Merged {newRecord.Name} record: {id}");
                                mergedObjects.Records.Add(newRecord);

                                foreach (string master in localUsedMasters)
                                {
                                    usedMasters.Add(master);
                                }

                                string masterList = string.Join(", ", GetFilteredLoadList(sortedMasters, localUsedMasters).Values.ToArray());
                                Logger.WriteLine($"Resolved conflicts for {firstRecord.Name} record '{id}' from mods: {masterList}");
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
                foreach (var gameFile in GetFilteredLoadList(sortedMasters, usedMasters).Values)
                {
                    if (usedMasters.Contains(gameFile))
                    {
                        long size = new FileInfo($"{morrowindPath}\\Data Files\\{gameFile}").Length;
                        mergedObjectsHeader.Masters.Add((new TES3Lib.Subrecords.TES3.MAST { Filename = $"{gameFile}\0" }, new TES3Lib.Subrecords.TES3.DATA { MasterDataSize = size }));
                    }
                }

                // Save out the merged objects file.
                mergedObjectsHeader.HEDR.NumRecords = mergedObjects.Records.Count - 1;
                mergedObjects.TES3Save(morrowindPath + "\\Data Files\\Merged Objects.esp");
                Logger.WriteLine($"Wrote {mergedObjects.Records.Count - 1} merged objects.");

                ShowCompletionPrompt();
            }
#if DEBUG == false
            catch (Exception e)
            {
                Console.WriteLine("A serious error has occurred. Please post the TES3Merge.log file to GitHub: https://github.com/NullCascade/TES3Merge/issues");
                Logger.WriteLine("An unhandled exception has occurred. Traceback:");
                Logger.WriteLine(e.Message);
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
