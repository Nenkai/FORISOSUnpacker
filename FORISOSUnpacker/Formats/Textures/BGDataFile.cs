using NenTools.IO.Streams;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FORISOSUnpacker.Formats.Textures;

public class BGDataFile
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RowStride { get; set; }
    public byte[] ImageData { get; set; }

    public static BGDataFile Open(Stream stream)
    {
        var bs = new SmartBinaryStream(stream);

        var bgdImage = new BGDataFile();
        bgdImage.OpenInternal(bs);
        return bgdImage;
    }

    private void OpenInternal(SmartBinaryStream bs)
    {
        int size = bs.ReadInt32();
        Width = bs.ReadInt32();
        Height = bs.ReadInt32();
        uint pad = bs.ReadUInt32();
        ImageData = bs.ReadBytes(size);

        RowStride = (Width * 3); // BGR24
    }

    public byte[] GetDecompressedImage()
    {
        byte[] outputBytes = new byte[RowStride * Height];
        DecompressImage(Width, Height, ImageData, outputBytes);
        return outputBytes;
    }

    private static int[] delta = new int[48]
   {
     1, 2, 4, 8, 16, 38, 80, 170,
     -1, -2, -4, -8, -16, -38, -80, -170,

     2, 4, 6, 12, 24, 48, 96, 192,
     -2, -4, -6, -12, -24, -48, -96, -192,

     5, 10, 20, 30, 50, 80, 130, 210,
     -5, -10, -20, -30, -50, -80, -130, -210
   };

    private static int[] state = new int[48]
    {
      0, 0, 0, 1, 1, 1, 1, 1,
      0, 0, 0, 1, 1, 1, 1, 1,

      0, 0, 1, 1, 2, 2, 1, 1,
      0, 0, 1, 1, 2, 2, 1, 1,

      1, 1, 2, 2, 2, 2, 1, 1,
      1, 1, 2, 2, 2, 2, 1, 1
    };

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

        int accA = 0;
        int stateA = 1;
        int accB = 0;
        int stateB = 1;
        int accC = 0;
        int stateC = 1;

        Span<byte> deltaBytes = MemoryMarshal.AsBytes(delta.AsSpan());
        Span<byte> stateBytes = MemoryMarshal.AsBytes(state.AsSpan());

        int pixelsToProcess = (width * height) / 2;
        for (int i = 0; i < pixelsToProcess; i++)
        {
            // Byte 0
            int v10 = (input[0] & 0xF) + 16 * stateB;
            int v11 = ((input[0] >> 4) & 0xF) + 16 * stateA;
            int v12 = state[v11];
            int v25 = delta[v11] + accA;
            int v13 = delta[v10] + accB;
            int v14 = state[v10];
            output[1] = (byte)(delta[v11] + accA);
            output[0] = (byte)(delta[v10] + accB);

            // Byte 1
            int v16 = (input[1] & 0xF) + 16 * stateC;
            int v17 = delta[v16] + accC;
            int v18 = ((input[1] >> 4) & 0xF) + 16 * state[v16];
            output[2] = (byte)(delta[v16] + accC);
            v18 *= 4;
            int v19 = BitConverter.ToInt32(deltaBytes.Slice(v18, 4));
            int v20 = BitConverter.ToInt32(stateBytes.Slice(v18, 4));
            int v21 = v19 + v17;
            output[5] = (byte)v21;
            accC = v21;
            stateC = v20;

            // Byte 2
            int v23 = ((input[2] >> 4) & 0xF) + 16 * v12;
            int v24 = (input[2] & 0xF) + 16 * v14;

            accA = delta[v23] + v25;
            stateA = state[v23];
            accB = delta[v24] + v13;
            stateB = state[v24];
            output[4] = (byte)accA;
            output[3] = (byte)accB;

            output = output[6..];
            input = input[3..];
        }
    }
}
