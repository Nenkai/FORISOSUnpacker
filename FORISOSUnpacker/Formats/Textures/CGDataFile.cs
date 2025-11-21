using NenTools.IO.Streams;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace FORISOSUnpacker.Formats.Textures;

public class CGDataFile
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RowStride { get; set; }
    public byte[] ImageData { get; set; }

    static CGDataFile()
    {
        GenLookupTable();
    }

    public static CGDataFile Open(Stream stream)
    {
        var bs = new SmartBinaryStream(stream);

        var bgdImage = new CGDataFile();
        bgdImage.OpenInternal(bs);
        return bgdImage;
    }

    private void OpenInternal(SmartBinaryStream bs)
    {
        int fileSize = bs.ReadInt32(); // EV33b.CGD reports 0x86914, but actual size is 0x86910?
        Width = bs.ReadInt32();
        Height = bs.ReadInt32();
        int unkOffsetMaybe = bs.ReadInt32();
        ImageData = bs.ReadBytes(unkOffsetMaybe - 0x10);

        RowStride = (Width * 3); // BGR24
    }

    private static sbyte[] LookupTable = new sbyte[32 * 32 * 32 * 3];
    private static void GenLookupTable()
    {
        Span<sbyte> lutPtr = LookupTable.AsSpan();
        for (int i = 0; i < 0x8000; i++)
        {
            sbyte rVal = (sbyte)((i >> 10) & 0b11111);
            sbyte gBal = (sbyte)((i >> 5) & 0b11111);
            sbyte bVal = (sbyte)(i & 0b11111);

            if (rVal > 0xF) rVal -= 32;
            if (gBal > 0xF) gBal -= 32;
            if (bVal > 0xF) bVal -= 32;

            lutPtr[0] = bVal;
            lutPtr[1] = gBal;
            lutPtr[2] = rVal;
            lutPtr = lutPtr[3..];
        }
    }

    public byte[] GetDecompressedImage()
    {
        byte[] outputBytes = new byte[RowStride * Height];
        DecompressImage(Width, Height, ImageData, outputBytes);
        return outputBytes;
    }

    /// <summary>
    /// Decompresses 3->6 block compressed image data
    /// </summary>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="inputBytes"></param>
    /// <param name="outputBytes"></param>
    private static void DecompressImage(int width, int height, byte[] inputBytes, byte[] outputBytes)
    {
        Span<byte> input = inputBytes.AsSpan();
        Span<byte> output = outputBytes.AsSpan();
        byte prevB = 0;
        byte prevG = 0;
        byte prevR = 0;

        while (true)
        {
            byte control = input[0];

            // delta
            if (control < 0x80)
            {
                ushort index = (ushort)(control << 8 | (input[1]));
                input = input[2..];

                int tableIndex = 3 * index;

                // Apply deltas from lookup table
                prevB = (byte)(prevB + LookupTable[tableIndex + 0]);
                prevG = (byte)(prevG + LookupTable[tableIndex + 1]);
                prevR = (byte)(prevR + LookupTable[tableIndex + 2]);

                // Write pixel
                output[0] = prevB;
                output[1] = prevG;
                output[2] = prevR;
                output = output[3..];
            }
            // rle - repeat
            else if (control < 0xC0)
            {
                int repeat = control - 0x7F;
                input = input[1..]; // consume control byte

                for (int i = 0; i < repeat; i++)
                {
                    output[0] = prevB;
                    output[1] = prevG;
                    output[2] = prevR;
                    output = output[3..];
                }
            }
            // raw
            else if (control != 0xFF)
            {
                byte count = (byte)(control + 65);
                input = input[1..]; // consume control byte

                for (int i = 0; i < count; i++)
                {
                    prevB = input[0];
                    prevG = input[1];
                    prevR = input[2];

                    output[0] = prevB;
                    output[1] = prevG;
                    output[2] = prevR;

                    input = input[3..];
                    output = output[3..];
                }
            }
            else
            {
                // End of stream (0xFF)
                break;
            }
        }
    }
}
