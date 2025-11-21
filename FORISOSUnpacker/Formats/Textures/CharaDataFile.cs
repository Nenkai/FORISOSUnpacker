using NenTools.IO.Streams;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FORISOSUnpacker.Formats.Textures;

public class CharaDataFile
{
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] ImageData { get; set; }
    public List<byte[]> MouthTextures { get; set; } = [];
    public short MouthScreenX { get; set; }
    public short MouthScreenY { get; set; }
    public short MouthScreenWidth { get; set; }
    public short MouthScreenHeight { get; set; }

    public static CharaDataFile Open(Stream stream)
    {
        var bs = new SmartBinaryStream(stream);

        var bgdImage = new CharaDataFile();
        bgdImage.OpenInternal(bs);
        return bgdImage;
    }

    private void OpenInternal(SmartBinaryStream bs)
    {
        const int HeaderSize = 0x20;

        int size = bs.ReadInt32();
        int mouthDataOffset = bs.ReadInt32(); // TODO. (int size/int/int/short ScreenX/short ScreenY/short Width/short Height/data follows...)
        Width = bs.ReadInt32();
        Height = bs.ReadInt32();
        int unk_0x10 = bs.ReadInt32();
        int unk_0x14 = bs.ReadInt32();
        bs.Position += 8;

        ImageData = bs.ReadBytes(size - HeaderSize);

        bs.Position = mouthDataOffset;
        int mouthDataSize = bs.ReadInt32();
        if (mouthDataSize > 0)
        {
            int mouthUnk_0x04 = bs.ReadInt32();
            int mouthNumImages = bs.ReadInt32();
            MouthScreenX = bs.ReadInt16();
            MouthScreenY = bs.ReadInt16();
            MouthScreenWidth = bs.ReadInt16();
            MouthScreenHeight = bs.ReadInt16();
            for (int i = 0; i < mouthNumImages; i++)
            {
                byte[] data = bs.ReadBytes(MouthScreenWidth * MouthScreenHeight * 4);
                MouthTextures.Add(data); // BGRA
            }
        }
    }

    public byte[] GetDecompressedImage()
    {
        byte[] outputBytes = new byte[(Width * Height) * 3]; // BGR24
        DecompressTexture(Width, Height, ImageData, outputBytes);
        return outputBytes;
    }

    private static void DecompressTexture(int width, int height, byte[] inputBytes, byte[] outputBytes)
    {
        Span<byte> input = inputBytes.AsSpan();

        Span<byte> output = outputBytes.AsSpan();

        for (int y = 0; y < height; y++)
        {
            Span<byte> outputRowPtr = output;

            while (true)
            {
                byte control = input[0];
                input = input[1..];
                if (control < 127)
                {
                    int factor = 0xFE - 2 * control;
                    int r = (factor * outputRowPtr[0]) >> 8;
                    int g = (factor * outputRowPtr[1]) >> 8;
                    int b = (factor * outputRowPtr[2]) >> 8;

                    outputRowPtr[0] = (byte)(input[0] + r);
                    outputRowPtr[1] = (byte)(input[1] + g);
                    outputRowPtr[2] = (byte)(input[2] + b);

                    input = input[3..];
                    outputRowPtr = outputRowPtr[3..];
                }
                else if (control < 0x9F)
                {
                    // Repeat pixels
                    int cnt = control - 0x7E;
                    for (int j = 0; j < cnt; j++)
                    {
                        outputRowPtr[0] = input[0];
                        outputRowPtr[1] = input[1];
                        outputRowPtr[2] = input[2];
                        outputRowPtr = outputRowPtr[3..];
                    }

                    input = input[3..];
                }
                else if (control < 0xFF)
                {
                    // Blank pixels
                    int cnt = control - 0x9E;
                    for (int j = 0; j < cnt; j++)
                        outputRowPtr = outputRowPtr[3..];
                }
                else if (control == 0xFF)
                    break;
            }

            output = output[(width * 3)..];
        }
    }
}
