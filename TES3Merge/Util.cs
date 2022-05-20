using IniParser;
using IniParser.Model;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using TES3Merge.BSA;

namespace TES3Merge
{
    internal static class Util
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
        internal static string? GetMorrowindFolder()
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
        /// Get a map of all physical files and bsa assets in the given directory
        /// </summary>
        /// <returns></returns>
        internal static ILookup<string, string> GetFileMap(string morrowindPath)
        {
            // map all files in Data Files
            WriteToLogAndConsole($"Generating file list ... ");
            var excludedExtensions = new List<string>() { ".esp", ".ESP", ".esm", ".bsa", ".json", ".exe", "", ".html", ".txt",
                ".css", ".lua", ".pdf", ".jpg", ".ini", ".md", ".mohidden", ".docx",
                ".7z",".rtf", ".log", ".psd", ".ods", ".csv", ".pkl", ".bak",
                ".luacheckrc", ".luacompleterc", ".cells", ".data", ".espf", ".esmf", ".dll"};


            var dataFiles = Path.Combine(morrowindPath, "Data Files");
            var physicalFiles = Directory
                .GetFiles(dataFiles, "*", SearchOption.AllDirectories)
                .Select(x => x.Substring(dataFiles.Length + 1))
                .Where(x => !excludedExtensions.Contains(Path.GetExtension(x)))
                .Select(x => x.ToLower());


            // TODO don't parse the ini twice
            // get Bsa files from ini
            {
                // Try to get INI information.
                IniData? data = null;
                var activatedBsas = new HashSet<string>
                {
                    "Morrowind.bsa"
                };
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

                        // do nothing and load all bsas
                        //ShowCompletionPrompt();
                        //return null;
                    }
                }

                // Build a list of activated files.
                if (data != null)
                {
                    var bsaIdx = 0;
                    while (true)
                    {
                        var archive = data["Archives"]["Archive " + bsaIdx];
                        if (string.IsNullOrEmpty(archive))
                        {
                            break;
                        }

                        // Add to masters list.
                        activatedBsas.Add(archive);
                        bsaIdx++;
                    }
                }

                var bsaFiles = Directory.GetFiles(dataFiles, "*.bsa", SearchOption.TopDirectoryOnly);
                foreach (var bsaFile in bsaFiles)
                {
                    if (activatedBsas != null && !activatedBsas.Contains(Path.GetFileName(bsaFile)))
                    {
                        continue;
                    }

                    using var fs = new FileStream(bsaFile, FileMode.Open);
                    var bsa = BsaParser.Read(fs);
                    if (bsa is not null)
                    {
                        var bsaAssets = bsa.Files.Select(x => x.Name);
                        physicalFiles = physicalFiles.Union(bsaAssets);
                    }

                }
            }

            var fileMap = physicalFiles.ToLookup(p => Path.GetExtension(p), p => p);

            return fileMap;
        }

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

        /// <summary>
        /// Get all plugins in the given directory path
        /// sorted by Morrowindf.ini
        /// </summary>
        /// <param name="morrowindPath"></param>
        /// <returns></returns>
        internal static IEnumerable<string>? GetSortedMasters(string morrowindPath)
        {
            ArgumentNullException.ThrowIfNull(Configuration);

            // Get the game file list from the ini file.
            var sortedMasters = new List<string>();
            Console.WriteLine("Parsing content files...");

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
                    return null;
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

            return sortedMasters;
        }
    }
}
