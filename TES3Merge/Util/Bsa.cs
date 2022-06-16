using TES3Merge.Extensions;

namespace TES3Merge.BSA;

public struct BSAHeader
{
    public uint version;
    public uint fileNameHashesOffset;
    public uint numFiles;
}

public struct BSAFileInfo
{
    public uint size;
    public uint offset;
}

public struct BSAHashRecord
{
    public uint value1;
    public uint value2;
}

public class BSARecord
{
    public BSARecord(BSAFile archive, string name, BSAFileInfo fileInfo, BSAHashRecord hash)
    {
        Archive = archive;
        Name = name;
        FileInfo = fileInfo;
        Hash = hash;
    }

    public BSAFile Archive { get; }
    public string Name;
    public BSAFileInfo FileInfo;
    public BSAHashRecord Hash;
}

public class BSAFile
{
    public List<BSARecord> Files = new();

    public DateTime ModificationTime { get; }

    public BSAFile(string path)
    {
        var info = new FileInfo(path);
        ModificationTime = info.LastWriteTime;

        using var stream = new FileStream(path, FileMode.Open);

        var header = stream.ReadStruct<BSAHeader>();

        var fileinfos = new List<BSAFileInfo>();
        for (var i = 0; i < header.numFiles; i++)
        {
            fileinfos.Add(stream.ReadStruct<BSAFileInfo>());
        }

        var fileNameOffsets = new List<uint>();
        for (var i = 0; i < header.numFiles; i++)
        {
            fileNameOffsets.Add(stream.ReadStruct<uint>());
        }

        var nameTableOffset = (uint)stream.Position;
        uint curOffset = 0;
        var fileNames = new List<string>();
        for (var i = 1; i < header.numFiles + 1; i++)
        {
            uint len;
            // last filename hack
            if (i != header.numFiles)
            {
                len = fileNameOffsets[i] - curOffset;
                curOffset = fileNameOffsets[i];
            }
            else
            {
                len = header.fileNameHashesOffset - (curOffset + nameTableOffset - 12);
            }

            var buffer = new byte[len];
            stream.Read(buffer);
            var s = System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length).TrimEnd('\0');
            fileNames.Add(s);
        }

        var fileHashes = new List<BSAHashRecord>();
        for (var i = 0; i < header.numFiles; i++)
        {
            fileHashes.Add(stream.ReadStruct<BSAHashRecord>());
        }

        for (var i = 0; i < header.numFiles; i++)
        {
            var record = new BSARecord(this, fileNames[i], fileinfos[i], fileHashes[i]);
            Files.Add(record);
        }
    }
};
