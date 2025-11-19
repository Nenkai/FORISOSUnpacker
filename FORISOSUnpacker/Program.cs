using System.Buffers.Binary;
using System.CommandLine;

using Microsoft.Extensions.Logging;
using NenTools.IO.Streams;
using NLog.Extensions.Logging;
using Syroot.BinaryData;

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
            _logger?.LogError(ex, "An error occurred while unpacking CMP file.");
        }
    }
}
