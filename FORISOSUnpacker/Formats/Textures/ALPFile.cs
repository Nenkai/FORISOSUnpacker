using NenTools.IO.Streams;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FORISOSUnpacker.Formats.Textures;

public class ALPFile
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] ImageData { get; set; }

    public static ALPFile Open(Stream stream)
    {
        var bs = new SmartBinaryStream(stream);

        var alpFile = new ALPFile();
        alpFile.OpenInternal(bs);
        return alpFile;
    }

    private void OpenInternal(SmartBinaryStream bs)
    {
        Width = bs.ReadInt32();
        Height = bs.ReadInt32();
        ImageData = bs.ReadBytes(Width * Height); // R8
    }
}
