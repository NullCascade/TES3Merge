using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TES3Lib;
using TES3Lib.Base;
using static TES3Merge.Util;

namespace TES3Merge
{
    // todo make this a proper commamd?
    internal static class VerifyCommand
    {
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
                using var ssw = new ScopedStopwatch();
                LoadConfig();
                ArgumentNullException.ThrowIfNull(Configuration);

                // Find out where Morrowind lives.
                var morrowindPath = GetMorrowindFolder();
                if (string.IsNullOrEmpty(morrowindPath))
                {
                    throw new Exception($"ERROR: Could not resolve Morrowind directory. Install TES3Merge into Morrowind\\TES3Merge\\TES3Merge.exe or reinstall Morrowind to fix registry values.");
                }
                Logger.WriteLine($"Morrowind found at '{morrowindPath}'.");

                // get merge tags
                var (supportedMergeTags, objectIdFilters) = GetMergeTags();

                // get all loaded plugins
                var sortedMasters = GetSortedMasters(morrowindPath);
                if (sortedMasters is null)
                {
                    return;
                }

                // get all physical and bsa files
                var fileMap = GetFileMap(morrowindPath);
                var extensionToFolderMap = GetExtensionMap(fileMap);


                // Go through and build a record list.
                var reportDict = new ConcurrentDictionary<string, Dictionary<string, List<string>>>();
                WriteToLogAndConsole($"Parsing plugins ... ");
                // make Parallel
                //foreach (var sortedMaster in sortedMasters)
                Parallel.ForEach(sortedMasters, sortedMaster =>
                {
                    // this can be enabled actually
                    if (Path.GetExtension(sortedMaster) == ".esm")
                    {
                        return;
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
                        GetPathsInRecord(record, map, fileMap, extensionToFolderMap);
                    }

                    if (map.Count > 0)
                    {
                        reportDict.AddOrUpdate(sortedMaster, map, (key, oldValue) => map);


                        //reportDict.Add(x =>  sortedMaster, map);
                    }
                });

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
                // serialize to file
                WriteToLogAndConsole($"\n");
                var reportPath = Path.Combine(morrowindPath, "Data Files", "report.json");
                WriteToLogAndConsole($"Writing report to: {reportPath}");
                {
                    using var fs = new FileStream(reportPath, FileMode.Create);
                    JsonSerializer.Serialize(fs, reportDict, new JsonSerializerOptions() { WriteIndented = true });
                }
            }
#if DEBUG == false
            catch (Exception e)
            {
                Console.WriteLine("A serious error has occurred. Please post the TES3Merge.log file to GitHub: https://github.com/NullCascade/TES3Merge/issues");
                Logger.WriteLine("An unhandled exception has occurred. Traceback:");
                Logger.WriteLine(e.Message);
                Logger.WriteLine(e.StackTrace);
                
            }
#endif

            ShowCompletionPrompt();
        }

        /// <summary>
        /// loop through all subrecords of a record 
        /// </summary>
        /// <param name="record"></param>
        /// <param name="map"></param>
        /// <param name="fileMap"></param>
        private static void GetPathsInRecord(
                Record record,
                Dictionary<string, List<string>> map,
                ILookup<string, string> fileMap,
                Dictionary<string, List<string>> extensionToFolderMap)
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
                    GetPathsInSubRecordRecursive(subrecord, recordDict, fileMap, extensionToFolderMap);
                }
            }

            if (recordDict.Count > 0)
            {
                var id = record.GetEditorId().TrimEnd('\0');
                map.Add(id, recordDict);
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
            ILookup<string, string> fileMap,
            Dictionary<string, List<string>> extensionToFolderMap)
        {

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
                        GetPathsInSubRecordRecursive(subrecord, map, fileMap, extensionToFolderMap);
                    }
                }
            }
        }
    }
}
