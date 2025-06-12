using IniParser;
using IniParser.Model;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using TES3Merge.BSA;

namespace TES3Merge.Util;

[Flags]
public enum EPatch
{
    None = 0,
    Fogbug = 1,
    Cellnames = 2,
    Summons = 4,
    All = 8,
}

internal static class Util
{
    public static StreamWriter Logger = new("TES3Merge.log", false)
    {
        AutoFlush = true
    };

    public static Installation? CurrentInstallation;

    public static IniData? Configuration;

    /// <summary>
    /// Generates a extension to folder map of the modded game
    /// </summary>
    /// <param name="fileMap"></param>
    /// <returns></returns>
    internal static Dictionary<string, List<string>> GetExtensionMap(ILookup<string, string> fileMap)
    {
        var excludedFolders = new List<string>() { "docs", "distantland", "mwse", "extras", "mash" };

        // generate the extensionMap
        var extensionToFolderMap = new Dictionary<string, List<string>>();
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

        return extensionToFolderMap;
    }

    /// <summary>
    /// Returns a list that is a copy of the load order, filtered to certain results.
    /// </summary>
    /// <param name="loadOrder">The base sorted load order collection.</param>
    /// <param name="filter">The filter to include elements from.</param>
    /// <returns>A copy of <paramref name="loadOrder"/>, filtered to only elements that match with <paramref name="filter"/>.</returns>
    internal static List<string> GetFilteredLoadList(IEnumerable<string> loadOrder, IEnumerable<string> filter)
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
    /// CLI completion
    /// </summary>
    internal static void ShowCompletionPrompt()
    {
        if (Configuration is not null && bool.TryParse(Configuration["General"]["PauseOnCompletion"], out var pauseOnCompletion) && pauseOnCompletion)
        {
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    /// <summary>
    /// Writes to both the console and the log file.
    /// </summary>
    /// <param name="Message">Message to write.</param>
    internal static void WriteToLogAndConsole(string Message)
    {
        Logger.WriteLine(Message);
        Console.WriteLine(Message);
    }

    /// <summary>
    /// Load this application's configuration.
    /// </summary>
    internal static void LoadConfig()
    {
        {
            var parser = new FileIniDataParser();
            var iniPath = Path.Combine(System.IO.Directory.GetCurrentDirectory(), "TES3Merge.ini");

            if (!File.Exists(iniPath))
                iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TES3Merge.ini");

            if (!File.Exists(iniPath))
                iniPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tes3merge", "TES3Merge.ini");

            if (!File.Exists(iniPath))
            {
                throw new Exception("TES3Merge was unable to locate a configuration file in any possible location. Aborting.");
            }

            Configuration = parser.ReadFile(iniPath);
        }

        // Determine what encoding to use.
        try
        {
            var iniEncodingCode = Configuration["General"]["TextEncodingCode"];
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

    }

    /// <summary>
    /// GetMergeTags from config and ini
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    internal static (List<string> supportedMergeTags, List<KeyValuePair<string, bool>> objectIdFilters) GetMergeTags()
    {
        ArgumentNullException.ThrowIfNull(Configuration);

        // TODO refactor this?
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
                    "CELL",
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
                    "LEVC",
                    "LEVI",
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

        // Make sure we're going to merge something.
        if (supportedMergeTags.Count == 0)
        {
            WriteToLogAndConsole("ERROR: No valid record types to merge. Check TES3Merge.ini's configuration.");
            throw new ArgumentException();
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

        return (supportedMergeTags, objectIdFilters);
    }
}
