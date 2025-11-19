using Microsoft.Extensions.Logging;

using NenTools.IO.Streams;

using NLog.Extensions.Logging;

using Syroot.BinaryData;

using System.Buffers.Binary;
using System.CommandLine;

namespace FORISOSUnpacker;

internal class Program
{
    private static ILoggerFactory? _loggerFactory;
    private static ILogger? _logger;

    static int Main(string[] args)
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        _logger = _loggerFactory.CreateLogger<Program>();

        Console.WriteLine("-----------------------------------------");
        Console.WriteLine($"- FORISOSUnpacker by Nenkai");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("- https://github.com/Nenkai");
        Console.WriteLine("- https://twitter.com/Nenkaai");
        Console.WriteLine("-----------------------------------------");
        Console.WriteLine("");

        // Game contents are in PACK files (.CMP extension), which also happen to be executables (but don't do anything).
        // They're UPX packed, but that doesn't matter too.

        // The executable/dll responsible for file system operations + decryption/decompression is FileSystem.mod.
        // There's an encryption for the toc of each pack (aka .CMP file, which btw are also executables but don't do anything).

        // The decryption key is part of the main executable. (ssds.exe).
        // Execution goes as such: ssds.exe -> MainSystem.dll -> FileSystem.mod.

        // Encryption key is set with the last exposed function of FileSystem
        // - offset: 10003D80 & 10004130, sig: 8B 44 24 ? 8B 08 89 0D

        // Open cmp file pattern: 6A ? 68 ? ? ? ? 64 A1 ? ? ? ? 50 64 89 25 ? ? ? ? 83 EC ? A1 ? ? ? ? 53 55

        var rootCommand = new RootCommand("FORISOSUnpacker");

        var inputPkzOption = new Option<FileInfo>("--input", "-i") { Required = true, Description = "Input .CMP file" };
        var outputOption = new Option<FileInfo?>("--output", "-o") { Description = "Output folder. Will default to file name + '_extracted' if not provided." };

        var extractPkzCommand = new Command("extract", "Extracts all files from a single .cmp archive.") { inputPkzOption, outputOption };
        extractPkzCommand.SetAction(parseResult =>
        {
            UnpackCmp(parseResult.GetRequiredValue(inputPkzOption), parseResult.GetValue(outputOption));
        });
        rootCommand.Subcommands.Add(extractPkzCommand);
        return rootCommand.Parse(args).Invoke();
    }

    private static void UnpackCmp(FileInfo inputFile, FileInfo? outputDirInfo)
    {
        string outputDir;
        if (outputDirInfo is not null)
            outputDir = outputDirInfo.FullName;
        else if (inputFile.DirectoryName is not null)
            outputDir = inputFile.DirectoryName;
        else
            outputDir = "extracted";

        try
        {
            var file = File.OpenRead(inputFile.FullName);
            var packFile = PackFile.Open(file, _loggerFactory);

            packFile.ExtractAll(outputDir);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "An error occurred while unpacking the CMP file.");
        }
    }
}
