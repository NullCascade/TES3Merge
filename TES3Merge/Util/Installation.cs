using IniParser;
using IniParser.Model;
using Microsoft.Win32;
using System.Runtime.InteropServices;
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

    /// <summary>
    /// An internal flag for if the installation was successfully loaded.
    /// </summary>
    internal bool Valid { get; set; }

    public Installation(string path)
    {
        Valid = true;
        RootDirectory = path;
    }

    /// <summary>
    /// Attempts to create an <see cref="Installation"/> object. It checks the current folder, then
    /// all parent folders for a valid Morrowind or OpenMW installation. If it does not find
    /// anything, it will fall back to registry values (Windows-only).
    /// </summary>
    /// <returns>The context-aware installation interface.</returns>
    public static Installation? CreateFromContext()
    {
        Installation? install = null;

        try
        {
            // Search all parent directories for Morrowind/OpenMW.
            for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Morrowind.exe")))
                {
                    install = new MorrowindInstallation(directory.FullName);
                    break;
                }
                else if (File.Exists(Path.Combine(directory.FullName, "openmw.exe")))
                {
                    install = new OpenMWInstallation(directory.FullName);
                    break;
                }
            }

            // On windows, fall back to the registry for Morrowind.
            if (install is null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var registryValue = Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\WOW6432Node\\bethesda softworks\\Morrowind", "Installed Path", null) as string;
                if (!string.IsNullOrEmpty(registryValue) && File.Exists(Path.Combine(registryValue, "Morrowind.exe")))
                {
                    install = new MorrowindInstallation(registryValue);
                }
            }
        }
        catch (Exception e)
        {
            if (install is not null)
            {
                install.Valid = false;
            }
            Util.Logger.WriteLine(e.Message);
        }

        if (install is not null && install.Valid)
        {
            return install;
        }

        return null;
    }

    /// <summary>
    /// This function is responsible for loading all the files, taking into consideration anything
    /// like MO2 or OpenMW's VFS.
    /// </summary>
    protected abstract void LoadDataFiles();

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
        BuildBSAList();
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
    private void BuildBSAList()
    {
        ArgumentNullException.ThrowIfNull(Configuration);

        // Always start off with Morrowind.
        Archives.Add("Morrowind.bsa");

        // Load the rest from the ini file.
        var configArchives = Configuration["Archives"];
        for (var i = 0; true; ++i)
        {
            var archive = configArchives["Archive " + i];
            if (string.IsNullOrEmpty(archive)) {
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
}

/// <summary>
/// Represents an installation specific to the normal OpenMW game engine. Extra considerations
/// include:
///  * Configuration format differs from normal.
///  * Multiple Data Files folders are supported at once.
/// </summary>
public class OpenMWInstallation : Installation
{
    public OpenMWInstallation(string path) : base(path)
    {
        throw new NotImplementedException();
    }

    protected override void LoadDataFiles()
    {
        throw new NotImplementedException();
    }
}
