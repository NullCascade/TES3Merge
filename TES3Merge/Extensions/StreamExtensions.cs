using System.Runtime.InteropServices;

namespace TES3Merge.Extensions;

public static class StreamExtensions
{
    public static byte[] ToByteArray(this Stream input, bool keepPosition = false)
    {
        if (input is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }
        else
        {
            using var ms = new MemoryStream();
            if (!keepPosition)
            {
                input.Position = 0;
            }
            input.CopyTo(ms);
            return ms.ToArray();

        }
    }

    public static T ReadStruct<T>(this Stream m_stream) where T : struct
    {
        var size = Marshal.SizeOf<T>();

        var m_temp = new byte[size];
        m_stream.Read(m_temp, 0, size);

        var handle = GCHandle.Alloc(m_temp, GCHandleType.Pinned);
        T item = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());

        handle.Free();

        return item;
    }

    public static void WriteStruct<T>(this Stream m_stream, T value) where T : struct
    {
        var m_temp = new byte[Marshal.SizeOf<T>()];
        var handle = GCHandle.Alloc(m_temp, GCHandleType.Pinned);

        Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), true);
        m_stream.Write(m_temp, 0, m_temp.Length);

        handle.Free();
    }

    public static T[] ReadStructs<T>(this Stream m_stream, uint count) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var items = new T[count];

        var m_temp = new byte[size];
        for (uint i = 0; i < count; i++)
        {
            m_stream.Read(m_temp, 0, size);

            var handle = GCHandle.Alloc(m_temp, GCHandleType.Pinned);
            items[i] = Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());

            handle.Free();
        }

        return items;
    }

    public static void WriteStructs<T>(this Stream m_stream, T[] array) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var m_temp = new byte[size];
        for (var i = 0; i < array.Length; i++)
        {
            var handle = GCHandle.Alloc(m_temp, GCHandleType.Pinned);

            Marshal.StructureToPtr(array[i], handle.AddrOfPinnedObject(), true);
            m_stream.Write(m_temp, 0, m_temp.Length);

            handle.Free();
        }
    }
}
