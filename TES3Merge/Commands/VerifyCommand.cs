/*
 * TODO
 * 
 * check NIF paths for each esp
 *
 */

using System.Collections.Concurrent;
using System.CommandLine;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using TES3Lib;
using TES3Lib.Base;
using static TES3Merge.Util.Util;

namespace TES3Merge.Commands;

public class VerifyCommand : Command
{
    private new const string Description = "Checks esps for missing file paths";
    private new const string Name = "verify";

    public VerifyCommand() : base(Name, Description)
    {
        this.SetHandler(() => VerifyAction.Run());
    }
}

internal static class VerifyAction
{
    /// <summary>
    /// Main command wrapper
    /// </summary>
    internal static void Run()
    {
#if DEBUG == false
    try
#endif
        {
            Verify();
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
    /// Verifies all active esps in the current Morrowind directory
    /// Parses all enabled records of the plugin and checks paths if the file exists
    /// </summary>
    /// <exception cref="Exception"></exception>
    private static void Verify()
    {
        ArgumentNullException.ThrowIfNull(CurrentInstallation);

        using var ssw = new ScopedStopwatch();
        LoadConfig();
        ArgumentNullException.ThrowIfNull(Configuration);

        // get merge tags
        var (supportedMergeTags, objectIdFilters) = GetMergeTags();

        // Shorthand install access.
        var sortedMasters = CurrentInstallation.GameFiles;

        // Go through and build a record list.
        var reportDict = new ConcurrentDictionary<string, Dictionary<string, List<string>>>();
        WriteToLogAndConsole($"Parsing plugins ... ");
        //foreach (var sortedMaster in sortedMasters)
        Parallel.ForEach(sortedMasters, sortedMaster =>
        {
            // this can be enabled actually
            if (Path.GetExtension(sortedMaster) == ".esm")
            {
                //continue;
                return;
            }

            var map = new Dictionary<string, List<string>>();

            // go through all records
            WriteToLogAndConsole($"Parsing input file: {sortedMaster}");
            var fullGameFilePath = Path.Combine(CurrentInstallation.RootDirectory, "Data Files", $"{sortedMaster}");
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
                GetPathsInRecord(record, map);
            }

            if (map.Count > 0)
            {
                reportDict.AddOrUpdate(sortedMaster, map, (key, oldValue) => map);
            }
        }
        );

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
        var reportPath = Path.Combine(CurrentInstallation.RootDirectory, "Data Files", "report.json");
        WriteToLogAndConsole($"Writing report to: {reportPath}");
        {
            using var fs = new FileStream(reportPath, FileMode.Create);
            JsonSerializer.Serialize(fs, reportDict, new JsonSerializerOptions() { WriteIndented = true });
        }

    }

    /// <summary>
    /// loop through all subrecords of a record 
    /// </summary>
    /// <param name="record"></param>
    /// <param name="map"></param>
    /// <param name="fileMap"></param>
    private static void GetPathsInRecord(
            Record record,
            Dictionary<string, List<string>> map)
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
                GetPathsInSubRecordRecursive(subrecord, recordDict);
            }
        }

        if (recordDict.Count > 0 && record is not null)
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
        List<string> map)
    {
        ArgumentNullException.ThrowIfNull(CurrentInstallation);

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
                var file = CurrentInstallation.GetSubstitutingDataFile(str);
                if (file is null)
                {
                    map.Add(str);
                }
            }
            else
            {
                if (val is Subrecord subrecord)
                {
                    GetPathsInSubRecordRecursive(subrecord, map);
                }
            }
        }
    }
}
