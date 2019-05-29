using System;
using System.Collections.Generic;
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
        static bool ListsMatch(List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)> a, List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                var first = a[i];
                var second = b[i];
                if (first.INDX.Type != second.INDX.Type)
                {
                    return false;
                }

                if ((first.BNAM != null ? first.BNAM.EditorId : null) != (second.BNAM != null ? second.BNAM.EditorId : null))
                {
                    return false;
                }

                if ((first.CNAM != null ? first.CNAM.FemalePartName : null) != (second.CNAM != null ? second.CNAM.FemalePartName : null))
                {
                    return false;
                }
            }

            return true;
        }

        static bool MergeProperty(PropertyInfo property, object obj, object first, object next)
        {
            if (first == null && next != null)
            {
                property.SetValue(obj, property.GetValue(next));
                return true;
            }

            var firstValue = property.GetValue(first);
            var nextValue = property.GetValue(next);

            if (firstValue == null && nextValue == null)
            {
                return false;
            }

            if (firstValue == null && nextValue != null)
            {
                property.SetValue(obj, nextValue);
                return true;
            }

            if (property.PropertyType.Equals(typeof(List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)>)))
            {
                if (!ListsMatch(firstValue as List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)>, nextValue as List<(TES3Lib.Subrecords.ARMO.INDX INDX, TES3Lib.Subrecords.Shared.BNAM BNAM, TES3Lib.Subrecords.ARMO.CNAM CNAM)>))
                {
                    property.SetValue(obj, property.GetValue(next));
                    return true;
                }
            }
            else
            {
                if (!firstValue.Equals(nextValue))
                {
                    property.SetValue(obj, property.GetValue(next));
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
            if (!next.Flags.SequenceEqual(first.Flags))
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
                    if (nextValue == null && firstValue != null)
                    {
                        property.SetValue(record, null);
                        modified = true;
                    }
                    else if (firstValue == null)
                    {
                        property.SetValue(record, nextValue);
                        modified = true;
                    }
                    else
                    {
                        if (MergeSubrecord(currentValue, firstValue, nextValue))
                        {
                            modified = true;
                        }
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

        static void MergeArmorRecord(TES3Lib.Records.ARMO record, TES3Lib.Records.ARMO first, TES3Lib.Records.ARMO next)
        {
            if (first == next)
            {
                return;
            }

            if (next.MODL.ModelPath != first.MODL.ModelPath)
            {
                record.MODL.ModelPath = next.MODL.ModelPath;
            }
        }

        static void Main(string[] args)
        {
            // 
            string morrowindPath;
            if (File.Exists("Morrowind.exe"))
            {
                morrowindPath = Directory.GetCurrentDirectory();
            }
            else if (File.Exists("..\\Morrowind.exe"))
            {
                morrowindPath = Directory.GetParent(Directory.GetCurrentDirectory()).FullName;
            }
            else
            {
                string registryValue = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\bethesda softworks\\Morrowind", "Installed Path", null) as String;
                if (!string.IsNullOrEmpty(registryValue) && File.Exists(Path.Combine(registryValue, "Morrowind.exe")))
                {
                    morrowindPath = registryValue;
                }
                else
                {
                    throw new Exception("Could not find Morrowind path!");
                }
            }

            Console.WriteLine($"Morrowind found at '{morrowindPath}'.");

            // Create our merged object TES3 file.
            TES3 mergedObjects = new TES3();
            var mergedObjectsHeader = new TES3Lib.Records.TES3();
            mergedObjectsHeader.HEDR = new TES3Lib.Subrecords.TES3.HEDR();
            mergedObjectsHeader.HEDR.CompanyName = "TES3Merge";
            mergedObjectsHeader.HEDR.Description = $"Automatic merge generated at {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}.";
            mergedObjects.Records.Add(mergedObjectsHeader);

            // Get a list of supported mergable object types.
            List<string> supportedMergeTags = new List<string> { "ACTI", "ALCH", "APPA", "ARMO", "BOOK", "CLOT", "CONT", "CREA", "DOOR", "GMST", "MISC", "NPC_", "WEAP" };

            // Collections for managing our objects.
            Dictionary<string, Dictionary<string, List<TES3Lib.Base.Record>>> recordOverwriteMap = new Dictionary<string, Dictionary<string, List<TES3Lib.Base.Record>>>();

            // Get the game file list from the ini file.
            SortedList<int, string> sortedMasters = new SortedList<int, string>();
            Dictionary<TES3, string> masterFileNames = new Dictionary<TES3, string>();
            Dictionary<TES3Lib.Base.Record, TES3> recordMasters = new Dictionary<TES3Lib.Base.Record, TES3>();
            {
                var parser = new FileIniDataParser();
                var config = parser.Parser.Configuration;
                config.SkipInvalidLines = true;
                IniData data = parser.ReadFile(morrowindPath + "\\Morrowind.ini");

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

                        string editorId = record.GetEditorId();
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

            // Go through and build merged objects.
            HashSet<string> usedMasters = new HashSet<string>();
            foreach (var recordType in recordOverwriteMap.Keys)
            {
                var recordsMap = recordOverwriteMap[recordType];
                foreach (string id in recordsMap.Keys)
                {
                    var records = recordsMap[id];
                    if (records.Count > 1)
                    {
                        var firstRecord = records[0];
                        var lastRecord = records.Last();
                        var lastMaster = masterFileNames[recordMasters[lastRecord]];
                        TES3Lib.Base.Record newRecord = Activator.CreateInstance(lastRecord.GetType(), new object[] { lastRecord.SerializeRecord() }) as TES3Lib.Base.Record;
                        foreach (var record in records)
                        {
                            if (MergeRecord(newRecord, firstRecord, record))
                            {
                                usedMasters.Add(masterFileNames[recordMasters[record]]);
                            }
                        }

                        if (!lastRecord.SerializeRecord().SequenceEqual(newRecord.SerializeRecord()))
                        {
                            Console.WriteLine($"Merged {newRecord.Name} record: {id}");
                            mergedObjects.Records.Add(newRecord);
                        }
                    }
                }
            }

            // Add the necessary masters.
            mergedObjectsHeader.Masters = new List<(TES3Lib.Subrecords.TES3.MAST MAST, TES3Lib.Subrecords.TES3.DATA DATA)>();
            foreach (var gameFile in sortedMasters.Values)
            {
                if (usedMasters.Contains(gameFile))
                {
                    long size = new FileInfo($"{morrowindPath}\\Data Files\\{gameFile}").Length;
                    mergedObjectsHeader.Masters.Add((new TES3Lib.Subrecords.TES3.MAST { Filename = $"{gameFile}\0" }, new TES3Lib.Subrecords.TES3.DATA { MasterDataSize = size }));
                }
            }

            // Save out the merged objects file.
            mergedObjects.TES3Save(morrowindPath + "\\Data Files\\Merged Objects.esp");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
