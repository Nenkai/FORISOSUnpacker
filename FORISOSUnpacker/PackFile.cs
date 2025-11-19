using CommunityToolkit.HighPerformance;

using NenTools.IO.Streams;

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Syroot.BinaryData.Memory;
using Microsoft.Extensions.Logging;

namespace FORISOSUnpacker;

internal class PackFile : IDisposable
{
    // ssds.exe - S.S.D.S. ~Setsuna no Akogare~
    private static readonly byte[] Key = [0xF5, 0xCD, 0x8C, 0x8B, 0xDB, 0xD7, 0x82, 0x3D, 0x6F, 0x6B, 0xA4, 0xC7, 0x04, 0xD6, 0x6B, 0xBB];
    private static uint MAGIC => BinaryPrimitives.ReadUInt32LittleEndian("PACK"u8);

    private readonly ILogger? _logger;
    private static readonly byte[] CurrentKey = Key;

    public Dictionary<string, PackFileEntry> Files { get; set; } = [];
    private SmartBinaryStream _stream;

    private PackFile(SmartBinaryStream stream, ILoggerFactory? loggerFactory = null)
    {
        _stream = stream;

        _logger = loggerFactory?.CreateLogger<PackFile>();
    }

    public static PackFile Open(string path)
    {
        using var fs = File.OpenRead(path);
        return Open(fs);
    }

    public static PackFile Open(Stream stream, ILoggerFactory? loggerFactory = null)
    {
        SmartBinaryStream bs = new SmartBinaryStream(stream, stringCoding: Syroot.BinaryData.StringCoding.Int16CharCount);

        var packFile = new PackFile(bs, loggerFactory);
        packFile.ReadInternal(bs);
        return packFile;
    }

    private void ReadInternal(SmartBinaryStream bs)
    {
        int headerOffset = (int)bs.Length - 8;
        bs.Position = headerOffset;
        Span<byte> header = stackalloc byte[8];
        bs.ReadExactly(header);

        Crypt(header[0x00..0x04], CurrentKey);
        if (BinaryPrimitives.ReadInt32LittleEndian(header) != MAGIC)
            throw new InvalidDataException("Not a FORIS-OS Pack (CMP) file. Magic did not match PACK at end of file after decryption.");

        if (_logger?.IsEnabled(LogLevel.Information) == true)
            _logger.LogInformation("File is a valid PACK file.");

        int tocOffset = BinaryPrimitives.ReadInt32LittleEndian(header[0x04..0x08]);
        bs.Position = tocOffset;

        if (_logger?.IsEnabled(LogLevel.Information) == true)
            _logger.LogInformation("TOC Offset: TOC (0x{offset:X})", tocOffset);

        int decompressedTocSize = bs.ReadInt32();
        int tocSize = headerOffset - tocOffset - 4;
        byte[] tocBytes = bs.ReadBytes(tocSize);

        if (_logger?.IsEnabled(LogLevel.Information) == true)
            _logger.LogInformation("Decrypting TOC (0x{size:X} bytes)", tocBytes.Length);

        Crypt(tocBytes, CurrentKey);

        if (_logger?.IsEnabled(LogLevel.Information) == true)
            _logger.LogInformation("Decompressing TOC (0x{size:X} bytes)", decompressedTocSize);

        byte[] decompressedToc = new byte[decompressedTocSize];
        DecompressLZ(tocBytes, decompressedToc, decompressedTocSize);

        SpanReader sr = new SpanReader(decompressedToc);
        PackFileEntry? lastEntry = null;
        while (true)
        {
            var entry = new PackFileEntry();
            entry.Read(ref sr);

            if (lastEntry is not null)
            {
                int fileHeaderSize = (entry.CompressFlags & 1) != 0 ? 4 : 0; // If compressed, there's a 4-byte decompressed size at the start of the file stream
                int distance = (entry.FileOffset - lastEntry.FileOffset) - fileHeaderSize;
                lastEntry.CompressedSize = distance;
            }

            if (string.IsNullOrWhiteSpace(entry.FileName))
                break;

            Files.Add(entry.FileName, entry);
            lastEntry = entry;
        }

        if (_logger?.IsEnabled(LogLevel.Information) == true)
            _logger.LogInformation("Done parsing TOC, {numFiles} files.", Files.Count);

    }

    public void ExtractAll(string outputDir)
    {
        int i = 0;
        foreach (var kvp in Files)
        {
            if (_logger?.IsEnabled(LogLevel.Information) == true)
                _logger.LogInformation("[{i}/{numFiles}] Extracting: {name} @ 0x{offset:X}..0x{endPos:X}", i+1, Files.Count, kvp.Key, kvp.Value.FileOffset, kvp.Value.FileOffset + kvp.Value.CompressedSize);

            var entry = kvp.Value;
            ExtractFile(outputDir, entry);

            i++;
        }
    }

    private void ExtractFile(string outputDir, PackFileEntry entry)
    {
        string outPath = Path.Combine(outputDir, entry.FileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        _stream.Position = entry.FileOffset;

        if ((entry.CompressFlags & 0x1) != 0)
        {
            int decompressedSize = _stream.ReadInt32();
            byte[] fileData = _stream.ReadBytes(entry.CompressedSize);
            byte[] decompressedData = new byte[decompressedSize];
            DecompressLZ(fileData, decompressedData, decompressedSize);
            File.WriteAllBytes(outPath, decompressedData);
        }
        else
        {
            byte[] fileData = _stream.ReadBytes(entry.CompressedSize);
            File.WriteAllBytes(outPath, fileData);
        }
    }

    public static void Crypt(Span<byte> bytes, byte[] key)
    {
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] ^= key[i % 16];
    }

    public static void DecompressLZ(Span<byte> input, Span<byte> output, int size)
    {
        int remBytes = size;
        int inPos = 0;
        int outPos = 0;

        while (remBytes > 0)
        {
            byte bits = input[inPos++];

            if ((bits & 0x80) != 0)
            {
                int allBits = input[inPos] + (bits << 8);
                int backOffset = allBits & 0b111_1111_1111; // 11 bits
                inPos++;

                int partSize = ((allBits >> 10) & 0x1E) + 2;
                if (remBytes < partSize)
                    partSize = remBytes;
                remBytes -= partSize;

                int prev = outPos - backOffset - 1;

                for (int i = 0; i < partSize; i++)
                    output[outPos++] = output[prev++];
            }
            else
            {
                int partSize = bits + 1;
                if (remBytes < partSize)
                    partSize = remBytes;

                for (int i = 0; i < partSize; i++)
                    output[outPos++] = input[inPos++];

                remBytes -= partSize;
            }
        }
    }

    public void Dispose()
    {
        ((IDisposable)_stream)?.Dispose();
    }
}
