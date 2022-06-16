using TES3Merge.BSA;

namespace TES3Merge.Util;

abstract public class DataFile
{
    public enum FileType
    {
        Normal,
        Archive,
    }

    public FileType Type { get; }

    public DateTime ModificationTime { get; set; }

    public DataFile(FileType type)
    {
        Type = type;
    }
}

public class NormalDataFile : DataFile
{
    public string FilePath { get; }

    public NormalDataFile(string path) : base(FileType.Normal)
    {
        var info = new FileInfo(path) ?? throw new Exception($"No file info could be found for {FilePath}.");
        if (!info.Exists)
        {
            throw new ArgumentException($"No file exists at {path}.");
        }

        FilePath = info.FullName;
        ModificationTime = info.LastWriteTime;
    }
}

public class ArchiveDataFile : DataFile
{
    public BSARecord Record { get; }

    public ArchiveDataFile(BSARecord record) : base(FileType.Archive)
    {
        Record = record;
        ModificationTime = Record.Archive.ModificationTime;
    }
}
