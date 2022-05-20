/*
 * TODO
 * 
 * from tes3cmd
 * This option has the following sub-options which can be used to remove
 creatures and items (by exact id match) from all leveled lists in
 which they occur:

 --delete-creature*|--dc* <creature-id>
 --delete-item*|--di* <item-id>
 * 
 * 
 */

using System.Text.RegularExpressions;
using TES3Lib;
using static TES3Merge.Util;

namespace TES3Merge.Commands;

internal static class MergeCommand
{
    /// <summary>
    /// Main command wrapper
    /// </summary>
    /// <param name="inclusiveListMerge"></param>
    /// <param name="filterRecords"></param>
    /// <param name="ignoredRecords"></param>
    internal static void Run(bool inclusiveListMerge, IEnumerable<string> filterRecords, IEnumerable<string> ignoredRecords)
    {
#if DEBUG == false
        try
#endif
        {
            Merge(inclusiveListMerge, filterRecords, ignoredRecords);
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
    /// Merge specified records
    /// </summary>
    /// <param name="inclusiveListMerge"></param>
    /// <param name="filterRecords"></param>
    /// <param name="ignoredRecords"></param>
    /// <param name="fileName"></param>
    /// <exception cref="Exception"></exception>
    internal static void Merge(bool inclusiveListMerge, IEnumerable<string>? filterRecords, IEnumerable<string>? ignoredRecords, string fileName = "Merged Objects.esp")
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

        // get merge tags
        var (supportedMergeTags, objectIdFilters) = GetMergeTags();
        if (filterRecords != null && filterRecords.Any())
        {
            supportedMergeTags = supportedMergeTags.Intersect(filterRecords).ToList();
        }
        if (ignoredRecords != null && ignoredRecords.Any())
        {
            supportedMergeTags = supportedMergeTags.Except(ignoredRecords).ToList();
        }
        WriteToLogAndConsole($"Supported record types: {string.Join(", ", supportedMergeTags)}");

        // get all loaded plugins
        var sortedMasters = GetSortedMasters(morrowindPath);
        if (sortedMasters is null)
        {
            return;
        }

        // Collections for managing our objects.
        var recordOverwriteMap = new Dictionary<string, Dictionary<string, List<TES3Lib.Base.Record>>>();
        var mapTES3ToFileNames = new Dictionary<TES3, string>();
        var recordMasters = new Dictionary<TES3Lib.Base.Record, TES3>();
        // Go through and build a record list.
        foreach (var sortedMaster in sortedMasters)
        {
            var fullGameFilePath = Path.Combine(morrowindPath, "Data Files", $"{sortedMaster}");
            var lastWriteTime = File.GetLastWriteTime(fullGameFilePath);
            Logger.WriteLine($"Parsing input file: {sortedMaster} @ {lastWriteTime}");
            var file = TES3.TES3Load(fullGameFilePath, supportedMergeTags);
            mapTES3ToFileNames[file] = sortedMaster;

            foreach (var record in file.Records)
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
            var recordsMap = recordOverwriteMap[recordType];
            foreach (var id in recordsMap.Keys)
            {
                var records = recordsMap[id];
                if (records.Count <= 2)
                {
                    continue;
                }
                var firstRecord = records[0];
                var lastRecord = records.Last();
                var firstMaster = mapTES3ToFileNames[recordMasters[firstRecord]];
                var lastMaster = mapTES3ToFileNames[recordMasters[lastRecord]];

                var localUsedMasters = new HashSet<string>() { firstMaster, lastMaster };

                var lastSerialized = lastRecord.GetRawLoadedBytes();
                var newRecord = Activator.CreateInstance(lastRecord.GetType(), new object[] { lastSerialized }) as TES3Lib.Base.Record ?? throw new Exception("Could not create activator instance.");
                for (var i = records.Count - 2; i > 0; i--)
                {
                    var record = records[i];
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

                        var masterListArray = GetFilteredLoadList(sortedMasters, localUsedMasters);
                        masterListArray.Add(master);
                        var masterList = string.Join(", ", masterListArray);
                        WriteToLogAndConsole($"Failed to merge {firstRecord.Name} record '{id}' from mods: {masterList}");
                        foreach (var r in records)
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
                            foreach (var record in records)
                            {
                                var master = mapTES3ToFileNames[recordMasters[record]];
                                Logger.WriteLine($">> {master}: {BitConverter.ToString(record.GetRawLoadedBytes()).Replace("-", "")}");
                            }
                            Logger.WriteLine($">> {fileName}: {BitConverter.ToString(newSerialized).Replace("-", "")}");
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
                    foreach (var record in records)
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

        // Did we even merge anything?
        if (usedMasters.Count == 0)
        {
            WriteToLogAndConsole("No merges were deemed necessary. Aborting.");
            ShowCompletionPrompt();
            return;
        }

        // Add the necessary masters.
        Logger.WriteLine($"Saving {fileName} ...");
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
        mergedObjects.TES3Save(Path.Combine(morrowindPath, "Data Files", fileName));
        Logger.WriteLine($"Wrote {mergedObjects.Records.Count - 1} merged objects.");
    }
}
