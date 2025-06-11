using IniParser;
using IniParser.Model;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using TES3Merge.BSA;

namespace TES3Merge.Util;

/// <summary>
/// Represents an installation of the game. This is used to load and store settings, as well as
/// provide abstraction around game files to support OpenMW's VFS.
/// </summary>
public abstract class Installation
{
    /// <summary>
    /// The primary directory that the installation can be found at.
    /// </summary>
    public string RootDirectory { get; }

    /// <summary>
    /// A list of archives defined by the installation.
    /// </summary>
    public List<string> Archives { get; } = new();

    /// <summary>
    /// 
    /// </summary>
    public Dictionary<string, BSAFile> ArchiveFiles { get; } = new();

    /// <summary>
    /// A list of game files defined by the installation. These are sorted by their last modification time.
    /// </summary>
    public List<string> GameFiles { get; } = new();

    /// <summary>
    /// 
    /// </summary>
    protected Dictionary<string, DataFile>? DataFiles;

    public Installation(string path)
    {
        RootDirectory = path;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private static bool IsValidInstallationDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return File.Exists(Path.Combine(path, "Morrowind.exe")) || File.Exists(Path.Combine(path, "openmw.cfg"));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    private static string? GetContextAwareInstallPath()
    {
        ArgumentNullException.ThrowIfNull(Util.Configuration);

        // Do we have an explicit install in our config file?
        var explicitPath = Util.Configuration["General"]["InstallPath"];
        if (IsValidInstallationDirectory(explicitPath))
        {
            return explicitPath;
        }

        // Search all parent directories for Morrowind/OpenMW.
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            if (IsValidInstallationDirectory(directory.FullName))
            {
                return directory.FullName;
            }
        }

        // On windows, fall back to the registry for Morrowind.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var registryValue = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\bethesda softworks\\Morrowind", "Installed Path", null) as string;
            if (!string.IsNullOrEmpty(registryValue) && IsValidInstallationDirectory(registryValue))
            {
                return registryValue;
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to create an <see cref="Installation"/> object. It checks the current folder, then
    /// all parent folders for a valid Morrowind or OpenMW installation. If it does not find
    /// anything, it will fall back to registry values (Windows-only).
    /// </summary>
    /// <returns>The context-aware installation interface.</returns>
    public static Installation? CreateFromContext()
    {
        var path = GetContextAwareInstallPath();
        if (path is null)
        {
            throw new Exception("Could not determine installation location.");
        }

        try
        {
            if (File.Exists(Path.Combine(path, "Morrowind.exe")))
            {
                return new MorrowindInstallation(path);
            }
            else if (File.Exists(Path.Combine(path, "openmw.cfg")))
            {
                return new OpenMWInstallation(path);
            }
        }
        catch (Exception e)
        {
            Util.Logger.WriteLine(e.Message);
        }

        return null;
    }

    /// <summary>
    /// This function is responsible for loading all the files, taking into consideration anything
    /// like MO2 or OpenMW's VFS.
    /// </summary>
    protected abstract void LoadDataFiles();

    /// <summary>
    /// Gets the output path to write the result merged objects plugin to.
    /// </summary>
    /// <returns>The path to use for the installation</returns>
    public abstract string GetDefaultOutputDirectory();

    /// <summary>
    /// Fetches file information relative to the Data Files directory. This file may be a normal
    /// file, or it may be a record in a BSA archive. The files mappings here are overwritten so
    /// that the last modified file always wins.
    /// </summary>
    /// <param name="path">The path, relative to Data Files, to fetch.</param>
    /// <returns>The interface to the file.</returns>
    public DataFile? GetDataFile(string path)
    {
        if (DataFiles is null)
        {
            LoadDataFiles();
        }

        ArgumentNullException.ThrowIfNull(DataFiles);

        if (DataFiles.TryGetValue(path.ToLower(), out DataFile? file))
        {
            return file;
        }

        return null;
    }

    /// <summary>
    /// As <see cref="GetDataFile(string)"/>, but accounts for changes to paths that Morrowind
    /// expects. Some assets will fall back to another file, i.e. texture.tga -> texture.dds.
    /// </summary>
    /// <param name="path">The path, relative to Data Files, to fetch.</param>
    /// <returns>The interface to the file.</returns>
    public DataFile? GetSubstitutingDataFile(string path)
    {
        var raw = GetDataFile(path);
        if (raw is not null)
        {
            return raw;
        }

        // Look up valid substitutions.
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        return extension.ToLower() switch
        {
            "tga" or "bmp" => GetDataFile(Path.ChangeExtension(path, "dds")),
            _ => null,
        };
    }
}

/// <summary>
/// Represents an installation specific to the normal Morrowind game engine. There are few extra
/// considerations needed.
/// </summary>
public class MorrowindInstallation : Installation
{
    /// <summary>
    /// The deserialized contents of the Morrowind.ini file.
    /// </summary>
    public IniData? Configuration;

    public MorrowindInstallation(string path) : base(path)
    {
        LoadConfiguration();
        BuildArchiveList();
        BuildGameFilesList();
    }

    /// <summary>
    /// Loads the <see cref="MorrowindInstallation.Configuration"/> member. It also logs malformed
    /// ini formatting.
    /// </summary>
    private void LoadConfiguration()
    {
        try
        {
            var parser = new FileIniDataParser();
            Configuration = parser.ReadFile(Path.Combine(RootDirectory, "Morrowind.ini"));
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
                Configuration = parser.ReadFile(Path.Combine(RootDirectory, "Morrowind.ini"));

                // If the first pass fails, be more forgiving, but let the user know their INI has issues.
                Console.WriteLine("WARNING: Issues were found with your Morrowind.ini file. See TES3Merge.log for details.");
                Util.Logger.WriteLine($"WARNING: Could not parse Morrowind.ini with initial pass. Error: {firstTry.Message}");
            }
            catch (Exception secondTry)
            {
                Console.WriteLine("ERROR: Unrecoverable issues were found with your Morrowind.ini file. See TES3Merge.log for details.");
                Util.Logger.WriteLine($"ERROR: Could not parse Morrowind.ini with second pass. Error: {secondTry.Message}");
            }
        }
    }

    /// <summary>
    /// Fills out the <see cref="Installation.Archives"/> list by parsing Morrowind.ini.
    /// </summary>
    private void BuildArchiveList()
    {
        ArgumentNullException.ThrowIfNull(Configuration);

        // Always start off with Morrowind.
        Archives.Add("Morrowind.bsa");

        // Load the rest from the ini file.
        var configArchives = Configuration["Archives"];
        for (var i = 0; true; ++i)
        {
            var archive = configArchives["Archive " + i];
            if (string.IsNullOrEmpty(archive))
            {
                break;
            }
            Archives.Add(archive);
        }
    }

    /// <summary>
    /// Fills out the <see cref="Installation.GameFiles"/> list by parsing Morrowind.ini. The list
    /// is sorted such that esm files appear before esp files, with ordering by last modification.
    /// </summary>
    private void BuildGameFilesList()
    {
        ArgumentNullException.ThrowIfNull(Configuration);

        // Get the raw list.
        List<string> definedFiles = new();
        var configGameFiles = Configuration["Game Files"];
        for (var i = 0; true; ++i)
        {
            var gameFile = configGameFiles["GameFile" + i];
            if (string.IsNullOrEmpty(gameFile))
            {
                break;
            }
            definedFiles.Add(gameFile);
        }

        // Add ESM files first.
        var dataFiles = Path.Combine(RootDirectory, "Data Files");
        foreach (var path in Directory.GetFiles(dataFiles, "*.esm", SearchOption.TopDirectoryOnly).OrderBy(p => File.GetLastWriteTime(p).Ticks))
        {
            var fileName = Path.GetFileName(path);
            if (definedFiles.Contains(fileName))
            {
                GameFiles.Add(fileName);
            }
        }

        // Then add other content files.
        foreach (var path in Directory.GetFiles(dataFiles, "*.esp", SearchOption.TopDirectoryOnly).OrderBy(p => File.GetLastWriteTime(p).Ticks))
        {
            var fileName = Path.GetFileName(path);
            if (definedFiles.Contains(fileName))
            {
                GameFiles.Add(fileName);
            }
        }
    }

    /// <summary>
    /// Loops through all the archives defined in Morrowind.ini, and fetches record handles to any
    /// files that exist in them. If the BSA was modified before the normal file, the BSA takes
    /// priority.
    /// </summary>
    /// <exception cref="Exception"></exception>
    private void MapArchiveFiles()
    {
        ArgumentNullException.ThrowIfNull(DataFiles);

        foreach (var archive in Archives)
        {
            var archiveFile = GetDataFile(archive) as NormalDataFile ?? throw new Exception($"Archive '{archive}' could not be found.");
            var bsa = new BSAFile(archiveFile.FilePath);
            foreach (var contained in bsa.Files)
            {
                var existing = GetDataFile(contained.Name);
                if (existing is null || bsa.ModificationTime > existing.ModificationTime)
                {
                    DataFiles[contained.Name.ToLower()] = new ArchiveDataFile(contained);
                }
            }
            ArchiveFiles[archive.ToLower()] = bsa;
        }
    }

    /// <summary>
    /// Builds a list of all entries in Data Files, and stores them in the
    /// <see cref="Installation.DataFiles"/> map. This must be called perior to the mapping of
    /// BSA file contents.
    /// </summary>
    private void MapNormalFiles()
    {
        ArgumentNullException.ThrowIfNull(DataFiles);

        var dataFiles = Path.Combine(RootDirectory, "Data Files");
        var physicalFiles = Directory
            .GetFiles(dataFiles, "*", SearchOption.AllDirectories)
            .Where(x => !x.EndsWith(".mohidden"))
            .Where(x => !x.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
            .Select(x => x[(dataFiles.Length + 1)..]);

        foreach (var file in physicalFiles)
        {
            DataFiles[file.ToLower()] = new NormalDataFile(Path.Combine(dataFiles, file));
        }
    }

    protected override void LoadDataFiles()
    {
        DataFiles = new();

        MapNormalFiles();
        MapArchiveFiles();
    }

    public override string GetDefaultOutputDirectory()
    {
        return Path.Combine(RootDirectory, "Data Files");
    }
}

/// <summary>
/// Represents an installation specific to the normal OpenMW game engine. Extra considerations
/// include:
///  * Configuration format differs from normal.
///  * Multiple Data Files folders are supported at once.
/// </summary>
public class OpenMWInstallation : Installation
{
    private List<string> DataDirectories = new();
    private string? DataLocalDirectory;
    private string? ResourcesDirectory;
    private static readonly string DuplicateSeparatorPattern =
    $"[{Regex.Escape($"{Path.DirectorySeparatorChar}{Path.AltDirectorySeparatorChar}")}]+";

    public OpenMWInstallation(string path) : base(path)
    {
        LoadConfiguration(path);

        if (!string.IsNullOrEmpty(DataLocalDirectory))
        {
            DataDirectories.Add(DataLocalDirectory);

            if (!Directory.Exists(DataLocalDirectory))
                Directory.CreateDirectory(DataLocalDirectory);
        }

        if (!string.IsNullOrEmpty(ResourcesDirectory))
            DataDirectories.Insert(0, Path.Combine(ParseDataDirectory(path, ResourcesDirectory), "vfs"));
    }

    private static string GetDefaultConfigurationDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(myDocs, "My Games", "OpenMW");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".config", "openmw");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Preferences", "openmw");
        }

        throw new Exception("Could not determine configuration path.");
    }

    private static string GetDefaultUserDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(myDocs, "My Games", "OpenMW");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

            if (string.IsNullOrEmpty(dataHome))
                dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");

            return Path.Combine(dataHome, "openmw");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "openmw");
        }

        throw new Exception("Could not determine user data directory.");
    }

    private static string ParseDataDirectory(string configDir, string dataDir)
    {
        if (dataDir.StartsWith('"'))
        {
            var original = dataDir;
            dataDir = "";
            for (int i = 1; i < original.Length; i++)
            {
                if (original[i] == '&')
                    i++;
                else if (original[i] == '"')
                    break;
                dataDir += original[i];
            }
        }

        if (dataDir.StartsWith("?userdata?"))
            dataDir = dataDir.Replace("?userdata?", GetDefaultUserDataDirectory() + Path.PathSeparator);
        else if (dataDir.StartsWith("?userconfig?"))
            dataDir = dataDir.Replace("?userconfig?", GetDefaultConfigurationDirectory() + Path.PathSeparator);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            dataDir = dataDir.Replace('/', '\\');

        if (!Path.IsPathRooted(dataDir))
            dataDir = Path.GetFullPath(Path.Combine(configDir, dataDir));

        return dataDir;
    }

    private void LoadConfiguration(string configDir)
    {
        var configPath = Path.Combine(configDir, "openmw.cfg");
        if (!File.Exists(configPath))
        {
            throw new Exception("openmw.cfg does not exist at the path " + configPath);
        }

        List<string> subConfigs = new List<string> { };
        foreach (var line in File.ReadLines(configPath))
        {
            if (string.IsNullOrEmpty(line) || line.Trim().StartsWith("#")) continue;

            var tokens = line.Split('=', 2);

            if (tokens.Length < 2) continue;

            var key = tokens[0].Trim();
            var value = tokens[1].Trim();

            switch (key)
            {
                case "data":
                    DataDirectories.Add(ParseDataDirectory(configDir, value));
                    break;
                case "content":
                    if (value.ToLower().EndsWith(".omwscripts")) continue;
                    else if (GameFiles.Contains(value))
                        throw new Exception(value + " was listed as a content file by two configurations! The second one was: " + configDir);

                    GameFiles.Add(value);
                    break;
                case "fallback-archive":
                    Archives.Add(value);
                    break;
                case "data-local":
                    DataLocalDirectory = ParseDataDirectory(configDir, value);
                    break;
                case "config":
                    subConfigs.Add(ParseDataDirectory(configDir, value));
                    break;
                case "resources":
                    ResourcesDirectory = ParseDataDirectory(configDir, value);
                    break;
            }
        }

        foreach (string config in subConfigs)
            try
            {
                LoadConfiguration(ParseDataDirectory(configDir, config));
            }
            catch (Exception e)
            {
                Util.Logger.WriteLine("WARNING: Sub-configuration " + configDir + " does not contain an openmw.cfg, skipping due to: " + e);
            }
    }

    /// <summary>
    /// Loops through all the archives defined in Morrowind.ini, and fetches record handles to any
    /// files that exist in them. BSAs always lose priority to loose files.
    /// </summary>
    /// <exception cref="Exception"></exception>
    private void MapArchiveFiles()
    {
        ArgumentNullException.ThrowIfNull(DataFiles);

        foreach (var archive in Archives)
        {
            var archiveFile = GetDataFile(archive) as NormalDataFile ?? throw new Exception($"Archive '{archive}' could not be found.");
            var bsa = new BSAFile(archiveFile.FilePath);
            foreach (var contained in bsa.Files)
            {
                var existing = GetDataFile(contained.Name);
                if (existing is null)
                {
                    DataFiles[contained.Name.ToLower()] = new ArchiveDataFile(contained);
                }
            }
            ArchiveFiles[archive.ToLower()] = bsa;
        }
    }

    /// <summary>
    /// Builds a list of all entries in a data folder, and stores them in the
    /// <see cref="Installation.DataFiles"/> map. This must be called perior to the mapping of
    /// BSA file contents.
    /// </summary>
    private void MapNormalFiles(string dataFiles)
    {
        ArgumentNullException.ThrowIfNull(DataFiles);

        var physicalFiles = Directory
            .GetFiles(dataFiles, "*", SearchOption.AllDirectories)
            .Where(x => !x.EndsWith(".mohidden"))
            //.Where(x => !x.Contains(Path.DirectorySeparatorChar + ".git" + Path.DirectorySeparatorChar))
            .Select(x => Path.GetRelativePath(dataFiles, x));

        foreach (var file in physicalFiles)
        {
            DataFiles[file.ToLower()] = new NormalDataFile(Path.Combine(dataFiles, file));
        }
    }

    protected override void LoadDataFiles()
    {
        DataFiles = new();

        foreach (var dataFiles in DataDirectories)
        {
            MapNormalFiles(dataFiles);
        }
        MapArchiveFiles();
    }

    public override string GetDefaultOutputDirectory()
    {
        if (DataDirectories.Count == 0)
        {
            throw new Exception("No data directories defined. No default output directory could be resolved.");
        }

        // Just use the first data directory.
        return DataDirectories[0];
    }
}
