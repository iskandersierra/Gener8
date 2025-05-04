using System.Text;
using Spectre.Console;

namespace Gener8.Core.Tests;

public class CopyServiceTest
{
    private const string SourceDir = "source";
    private const string DestinationDir = "destination";

    [Theory]
    [ClassData(typeof(ExecuteData))]
    public async Task Execute(
        string name,
        bool outputDirectory,
        Dictionary<string, byte[]> sourceDir,
        Func<string, CopyRequest> makeRequest,
        bool expectedSuccess
    )
    {
        // Arrange
        await using var fs = new VirtualFs($"h4e56_{name}");
        Directory.CreateDirectory(Path.Combine(fs.RootPath, SourceDir));

        foreach (var pair in sourceDir)
        {
            var sourceFileName = Path.Combine(fs.RootPath, pair.Key);
            var sourceFileDirectory = Path.GetDirectoryName(sourceFileName)!;
            Directory.CreateDirectory(sourceFileDirectory);
            await using var sourceFile = File.Create(sourceFileName);
            await sourceFile.WriteAsync(pair.Value);
            await sourceFile.FlushAsync();
            sourceFile.Close();
        }

        await using var consoleOut = new StringWriter();

        var console = AnsiConsole.Create(
            new()
            {
                Out = new TestAnsiConsoleOutput(consoleOut),
                Ansi = AnsiSupport.No,
                ColorSystem = ColorSystemSupport.NoColors,
                Interactive = InteractionSupport.No,
            }
        );

        var service = new CopyService(console);

        var request = makeRequest(fs.RootPath);

        // Act
        var result = await service.ExecuteAsync(request);

        // Assert
        await Verify(consoleOut.ToString())
            .UseDirectory("_snapshots")
            .UseMethodName($"Execute_{name}_console");

        if (outputDirectory)
        {
            await VerifyDirectory(Path.Combine(fs.RootPath, DestinationDir))
                .UseDirectory("_snapshots")
                .UseMethodName($"Execute_{name}_dir");
        }
        else
        {
            new DirectoryInfo(Path.Combine(fs.RootPath, DestinationDir)).Exists.ShouldBeFalse();
        }

        result.ShouldBe(new CopyResult(expectedSuccess));
    }

    public class ExecuteData
        : TheoryData<string, bool, Dictionary<string, byte[]>, Func<string, CopyRequest>, bool>
    {
        public ExecuteData()
        {
            Add(
                "file_file",
                true,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/first.txt"] = "Some Content"u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new FileInfo(Path.Combine(rootPath, $"{SourceDir}/first.txt")),
                    Destination = new FileInfo(
                        Path.Combine(rootPath, $"{DestinationDir}/second.txt")
                    ),
                    Verbose = true,
                },
                true
            );

            Add(
                "file_directory",
                true,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/first.txt"] = "Some Content"u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new FileInfo(Path.Combine(rootPath, $"{SourceDir}/first.txt")),
                    Destination = new DirectoryInfo(Path.Combine(rootPath, DestinationDir)),
                    Verbose = true,
                },
                true
            );

            Add(
                "directory_file",
                false,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/first.txt"] = "Some Content"u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new DirectoryInfo(Path.Combine(rootPath, SourceDir)),
                    Destination = new FileInfo(
                        Path.Combine(rootPath, $"{DestinationDir}/second.txt")
                    ),
                    Verbose = true,
                },
                false
            );

            Add(
                "dry_run",
                false,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/first.txt"] = "Some Content"u8.ToArray(),
                    [$"{SourceDir}/children/second.cs"] = "// More Content"u8.ToArray(),
                    [$"{SourceDir}/children/third.json"] = """["More Stuff"]"""u8.ToArray(),
                    [$"{SourceDir}/ignore/fourth.cs"] = "// ignored"u8.ToArray(),
                    [$"{SourceDir}/ignore/fifth.txt"] = """Ignored Content"""u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new DirectoryInfo(Path.Combine(rootPath, SourceDir)),
                    Destination = new DirectoryInfo(Path.Combine(rootPath, DestinationDir)),
                    DryRun = true,
                    Verbose = true,
                    Include = ["*.json", "**/*.txt", "**/*.cs"],
                    Exclude = ["**/ignore/**/*"],
                },
                true
            );

            Add(
                "recursive",
                true,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/first.txt"] = "Some Content"u8.ToArray(),
                    [$"{SourceDir}/children/second.cs"] = "// More Content"u8.ToArray(),
                    [$"{SourceDir}/children/third.json"] = """["More Stuff"]"""u8.ToArray(),
                    [$"{SourceDir}/ignore/fourth.cs"] = "// ignored"u8.ToArray(),
                    [$"{SourceDir}/ignore/fifth.txt"] = """Ignored Content"""u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new DirectoryInfo(Path.Combine(rootPath, SourceDir)),
                    Destination = new DirectoryInfo(Path.Combine(rootPath, DestinationDir)),
                    Verbose = true,
                    Include = ["*.json", "**/*.txt", "**/*.cs"],
                    Exclude = ["**/ignore/**/*"],
                },
                true
            );

            Add(
                "no_recursive",
                true,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/first.txt"] = "Some Content"u8.ToArray(),
                    [$"{SourceDir}/children/second.cs"] = "// More Content"u8.ToArray(),
                    [$"{SourceDir}/children/third.json"] = """["More Stuff"]"""u8.ToArray(),
                    [$"{SourceDir}/fourth.cs"] = "// Fourth content"u8.ToArray(),
                    [$"{SourceDir}/fifth.txt"] = """Fifth Content"""u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new DirectoryInfo(Path.Combine(rootPath, SourceDir)),
                    Destination = new DirectoryInfo(Path.Combine(rootPath, DestinationDir)),
                    Recursive = false,
                    Verbose = true,
                    Include = ["*.json", "**/*.txt", "**/*.cs"],
                    Exclude = ["**/ignore/**/*"],
                },
                true
            );

            Add(
                "encoding",
                true,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/utf8-no-bom.txt"] = new UTF8Encoding(false).GetBytes(
                        "ʹ͵ͺ;΄΅Ά·ΈΉΊΌΎΏΐΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩΪΫάέήίΰαβγδεζηθικλμνξοπρςστυφχψωϊϋόύώϐϑϒϓϔϕϖϚϜϞϠϢϣϤϥϦϧϨϩϪϫϬϭϮϯϰϱϲϳ"
                    ),
                    [$"{SourceDir}/utf8-bom.txt"] = new UTF8Encoding(true).GetBytes(
                        "ʹ͵ͺ;΄΅Ά·ΈΉΊΌΎΏΐΑΒΓΔΕΖΗΘΙΚΛΜΝΞΟΠΡΣΤΥΦΧΨΩΪΫάέήίΰαβγδεζηθικλμνξοπρςστυφχψωϊϋόύώϐϑϒϓϔϕϖϚϜϞϠϢϣϤϥϦϧϨϩϪϫϬϭϮϯϰϱϲϳ"
                    ),
                    [$"{SourceDir}/ascii.txt"] = Encoding.ASCII.GetBytes(
                        "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~"
                    ),
                    [$"{SourceDir}/utf32.txt"] = Encoding.UTF32.GetBytes(
                        "अआइईउऊऋऌऍऎएऐऑऒओऔकखगघङचछजझञटठडढणतथदधनऩपफबभमयरऱलळऴवशषसहऽॐक़ख़ग़ज़ड़ढ़फ़य़ॠॡ।॥०१२३४५६७८९॰"
                    ),
                    [$"{SourceDir}/unicode.txt"] = Encoding.Unicode.GetBytes(
                        "अआइईउऊऋऌऍऎएऐऑऒओऔकखगघङचछजझञटठडढणतथदधनऩपफबभमयरऱलळऴवशषसहऽॐक़ख़ग़ज़ड़ढ़फ़य़ॠॡ।॥०१२३४५६७८९॰"
                    ),
                },
                rootPath => new CopyRequest
                {
                    Source = new DirectoryInfo(Path.Combine(rootPath, SourceDir)),
                    Destination = new DirectoryInfo(Path.Combine(rootPath, DestinationDir)),
                    Verbose = true,
                },
                true
            );

            Add(
                "overwrite_always",
                true,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/first.txt"] = "First Content"u8.ToArray(),
                    [$"{SourceDir}/second.txt"] = "Second Content"u8.ToArray(),
                    [$"{DestinationDir}/first.txt"] = "To overwrite"u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new DirectoryInfo(Path.Combine(rootPath, SourceDir)),
                    Destination = new DirectoryInfo(Path.Combine(rootPath, DestinationDir)),
                    Verbose = true,
                },
                true
            );

            Add(
                "overwrite_never",
                true,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/first.txt"] = "First Content"u8.ToArray(),
                    [$"{SourceDir}/second.txt"] = "Second Content"u8.ToArray(),
                    [$"{DestinationDir}/first.txt"] = "To overwrite"u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new DirectoryInfo(Path.Combine(rootPath, SourceDir)),
                    Destination = new DirectoryInfo(Path.Combine(rootPath, DestinationDir)),
                    Overwrite = OverwriteMode.Never,
                    Verbose = true,
                },
                true
            );

            Add(
                "replace_plain",
                true,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/First.txt"] = "First first content"u8.ToArray(),
                    [$"{SourceDir}/Second.txt"] = "Second second content"u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new DirectoryInfo(Path.Combine(rootPath, SourceDir)),
                    Destination = new DirectoryInfo(Path.Combine(rootPath, DestinationDir)),
                    Verbose = true,
                    Replace = new (string Key, string Value)[]
                    {
                        ("First", "Primer"),
                        ("Second", "Segundo"),
                        ("content", "contenido"),
                    }.ToLookup(e => e.Key, e => e.Value),
                },
                true
            );

            Add(
                "replace_regex",
                true,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/Archivo01.txt"] =
                        "File01 has some digits02 to separate03"u8.ToArray(),
                    [$"{SourceDir}/Sample_10.txt"] =
                        "Sample_10 is already\t20 separated 30"u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new DirectoryInfo(Path.Combine(rootPath, SourceDir)),
                    Destination = new DirectoryInfo(Path.Combine(rootPath, DestinationDir)),
                    Verbose = true,
                    Replace = new (string Key, string Value)[]
                    {
                        (@"\b([a-z]+?)(\d+)", "$1_$2"),
                    }.ToLookup(e => e.Key, e => e.Value),
                    ReplaceMode = ReplaceMode.Regex,
                    IgnoreCase = true,
                },
                true
            );
        }
    }
}
