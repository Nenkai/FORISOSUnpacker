using NenTools.IO.Streams;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FORISOSUnpacker.Formats.Textures;

public class TextureDataFile
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RowStride { get; set; }
    public byte[] ImageData { get; set; }

    public static TextureDataFile Open(Stream stream)
    {
        var bs = new SmartBinaryStream(stream);

        var textureDataFile = new TextureDataFile();
        textureDataFile.OpenInternal(bs);
        return textureDataFile;
    }

    private void OpenInternal(SmartBinaryStream bs)
    {
        Width = bs.ReadInt32();
        Height = bs.ReadInt32();
        ImageData = bs.ReadBytes(Width * Height * 4);

        RowStride = (Width * 4); // BGRA32
    }
}
