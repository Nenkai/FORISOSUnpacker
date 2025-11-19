using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Text;

namespace FORISOSUnpacker;

public class PackFileEntry
{
    public int FileOffset { get; set; }
    public byte CompressFlags { get; set; } // Bits
    public int Unk0x06 { get; set; }
    public string FileName { get; set; }
    public int CompressedSize { get; set; }

    public void Read(ref SpanReader sr)
    {
        // 10 bytes header
        FileOffset = sr.ReadInt32();
        byte nameLength = sr.ReadByte();
        CompressFlags = sr.ReadByte();
        Unk0x06 = sr.ReadInt32();

        Span<byte> strBuf = sr.ReadBytes(nameLength * 2);
        FileName = Encoding.Unicode.GetString(strBuf);
    }
}
