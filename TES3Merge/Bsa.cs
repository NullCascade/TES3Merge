using TES3Merge.Extensions;

namespace TES3Merge;

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

public struct HashRecord
{
    public uint value1;
    public uint value2;
}

public class BSARecord
{
    public BSARecord(string name, BSAFileInfo fileInfo, HashRecord hash)
    {
        Name = name;
        FileInfo = fileInfo;
        Hash = hash;
    }

    public string Name;
    public BSAFileInfo FileInfo;
    public HashRecord Hash;
}

public class BSAFile
{
    public List<BSARecord> Files;

    public BSAFile()
    {
        Files = new List<BSARecord>();
    }
};

public static class BsaParser
{
    public static BSAFile? Read(Stream stream)
    {
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
            uint len = 0;
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

        var fileHashes = new List<HashRecord>();
        for (var i = 0; i < header.numFiles; i++)
        {
            fileHashes.Add(stream.ReadStruct<HashRecord>());
        }

        var files = new List<BSARecord>();
        for (var i = 0; i < header.numFiles; i++)
        {
            var record = new BSARecord(fileNames[i], fileinfos[i], fileHashes[i]);
            files.Add(record);
        }

        var file = new BSAFile
        {
            Files = files
        };
        return file;
    }
}

