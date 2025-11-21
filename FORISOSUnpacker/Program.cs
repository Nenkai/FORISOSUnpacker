using System.Buffers.Binary;
using System.CommandLine;
using System.Runtime.InteropServices;

using Microsoft.Extensions.Logging;

using NenTools.IO.Streams;

using NLog.Extensions.Logging;

using Syroot.BinaryData;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using FORISOSUnpacker.Formats.Textures;
using FORISOSUnpacker.Formats.Pack;

namespace FORISOSUnpacker;

internal class Program
{
    private static ILoggerFactory? _loggerFactory;
    private static ILogger? _logger;

    static int Main(string[] args)
    {
        _loggerFactory = LoggerFactory.Create(builder => builder.AddNLog());
        _logger = _loggerFactory.CreateLogger<Program>();

        if (args.Length == 1)
        {
            if (Directory.Exists(args[0]))
            {
                foreach (var file in Directory.GetFiles(args[0]))
                {
                    ProcessFile(file);
                }

                return -1;
            }
            else if (File.Exists(args[0]))
            {
                ProcessFile(args[0]);
                return -1;
            }
        }

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

    private static void ProcessFile(string file)
    {
        if (Path.GetExtension(file) == ".BGD")
        {
            ConvertBGData(file); // GObj_BGData
        }
        else if (Path.GetExtension(file) == ".CGD")
        {
            ConvertCGData(file); // GObj_CGData
        }
        else if (Path.GetExtension(file) == ".CHR")
        {
            ConvertCHRData(file); // GObj_CHRData
        }
        else if (Path.GetExtension(file) == ".TEXB")
        {
            ConvertTEXBData(file);
        }
        else if (Path.GetExtension(file) == ".ALP")
        {
            ConvertALPData(file);
        }
        else
            _logger?.LogInformation("Skipping {file}", file);
    }

    private static void ConvertCHRData(string file)
    {
        var chr = CharaDataFile.Open(File.OpenRead(file));
        _logger?.LogInformation("CHR file {file} - {width}x{height} ({numMouthImages})", file, chr.Width, chr.Height, chr.MouthTextures.Count);

        if (chr.MouthTextures.Count > 0)
            _logger?.LogInformation("Mouth Data: ScreenPos <{screenX},{screenY}>, {width}x{height}", chr.MouthScreenX, chr.MouthScreenY, chr.MouthScreenWidth, chr.MouthScreenHeight);

        var decompressedImage = chr.GetDecompressedImage();

        Image<Bgr24> image = Image.LoadPixelData<Bgr24>(decompressedImage, chr.Width, chr.Height);

        _logger?.LogInformation("Flipping image...");
        image.Mutate(x => x.Flip(FlipMode.Vertical));

        string outDir = Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file));
        Directory.CreateDirectory(outDir);
        _logger?.LogInformation("Extracting at {outputDir}", outDir);

        _logger?.LogInformation("Saving as png...");
        image.SaveAsPng(Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + ".png"));

        for (int i = 0; i < chr.MouthTextures.Count; i++)
        {
            var mouthTexture = chr.MouthTextures[i];
            Image<Bgra32> mouthImage = Image.LoadPixelData<Bgra32>(mouthTexture, chr.MouthScreenWidth, chr.MouthScreenHeight);

            _logger?.LogInformation("Flipping mouth image {index}...", i);
            mouthImage.Mutate(x => x.Flip(FlipMode.Vertical));

            _logger?.LogInformation("Saving mouth image {index} as png...", i);
            mouthImage.SaveAsPng(Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(file)}_mouth_{i}.png"));
        }
    }

    private static void ConvertCGData(string file)
    {
        var cgd = CGDataFile.Open(File.OpenRead(file));
        _logger?.LogInformation("CGD file {file} - {width}x{height}", file, cgd.Width, cgd.Height);

        var decompressedImage = cgd.GetDecompressedImage();

        Image<Bgr24> image = Image.LoadPixelData<Bgr24>(decompressedImage, cgd.Width, cgd.Height);

        _logger?.LogInformation("Flipping image...");
        image.Mutate(x => x.Flip(FlipMode.Vertical));

        _logger?.LogInformation("Saving as png...");
        image.SaveAsPng(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".png"));
    }

    private static void ConvertBGData(string file)
    {
        var bgd = BGDataFile.Open(File.OpenRead(file));
        _logger?.LogInformation("BGD file {file} - {width}x{height}", file, bgd.Width, bgd.Height);

        var decompressedImage = bgd.GetDecompressedImage();

        Image<Bgr24> image = Image.LoadPixelData<Bgr24>(decompressedImage, bgd.Width, bgd.Height);

        _logger?.LogInformation("Flipping image...");
        image.Mutate(x => x.Flip(FlipMode.Vertical));

        _logger?.LogInformation("Saving as png...");
        image.SaveAsPng(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".png"));
    }

    private static void ConvertTEXBData(string file)
    {
        var texb = TextureDataFile.Open(File.OpenRead(file));
        _logger?.LogInformation("TEXB file {file} - {width}x{height}", file, texb.Width, texb.Height);

        Image<Bgra32> image = Image.LoadPixelData<Bgra32>(texb.ImageData, texb.Width, texb.Height);

        _logger?.LogInformation("Flipping image...");
        image.Mutate(x => x.Flip(FlipMode.Vertical));

        _logger?.LogInformation("Saving as png...");
        image.SaveAsPng(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".png"));
    }

    private static void ConvertALPData(string file)
    {
        var alpFile = ALPFile.Open(File.OpenRead(file));
        _logger?.LogInformation("ALP file {file} - {width}x{height}", file, alpFile.Width, alpFile.Height);

        Image<Rgba32> image = new Image<Rgba32>(alpFile.Width, alpFile.Height);
        for (int y = 0; y < alpFile.Height; y++)
        {
            for (int x = 0; x < alpFile.Width; x++)
            {
                byte color = alpFile.ImageData[(y * alpFile.Width) + x];
                image[x, y] = new Rgba32(color, color, color);
            }
        }

        _logger?.LogInformation("Flipping image...");
        image.Mutate(x => x.Flip(FlipMode.Vertical));

        _logger?.LogInformation("Saving as png...");
        image.SaveAsPng(Path.Combine(Path.GetDirectoryName(file), Path.GetFileNameWithoutExtension(file) + ".png"));
    }

    private static void UnpackCmp(FileInfo inputFile, FileInfo? outputDirInfo)
    {
        string outputDir;
        if (outputDirInfo is not null)
            outputDir = outputDirInfo.FullName;
        else if (inputFile.DirectoryName is not null)
            outputDir = Path.Combine(inputFile.DirectoryName, $"{Path.GetFileNameWithoutExtension(inputFile.Name)}_extracted");
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
