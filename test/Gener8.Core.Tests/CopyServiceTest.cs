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
        Func<string, CopyRequest> makeRequest
    )
    {
        // Arrange
        await using var fs = new VirtualFs($"h4e56_{name}");
        Directory.CreateDirectory(Path.Combine(fs.RootPath, SourceDir));

        foreach (var pair in sourceDir)
        {
            var sourceFileName = Path.Combine(fs.RootPath, pair.Key);
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
        await service.ExecuteAsync(request);

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
    }

    public class ExecuteData
        : TheoryData<string, bool, Dictionary<string, byte[]>, Func<string, CopyRequest>>
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
                    Recursive = true,
                    Overwrite = OverwriteMode.Always,
                    CreateDirectories = true,
                    ReplaceContent = false,
                    ReplaceNames = true,
                    DryRun = false,
                    Verbose = true,
                    Replace = Array.Empty<string>().ToLookup(e => e),
                    Encoding = null,
                    ReplaceMode = ReplaceMode.Plain,
                    Include = [],
                    Exclude = [],
                }
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
                    Recursive = true,
                    Overwrite = OverwriteMode.Always,
                    CreateDirectories = true,
                    ReplaceContent = false,
                    ReplaceNames = true,
                    DryRun = false,
                    Verbose = true,
                    Replace = Array.Empty<string>().ToLookup(e => e),
                    Encoding = null,
                    ReplaceMode = ReplaceMode.Plain,
                    Include = [],
                    Exclude = [],
                }
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
                    Recursive = true,
                    Overwrite = OverwriteMode.Always,
                    CreateDirectories = true,
                    ReplaceContent = false,
                    ReplaceNames = true,
                    DryRun = false,
                    Verbose = true,
                    Replace = Array.Empty<string>().ToLookup(e => e),
                    Encoding = null,
                    ReplaceMode = ReplaceMode.Plain,
                    Include = [],
                    Exclude = [],
                }
            );

            Add(
                "dry_run",
                true,
                new Dictionary<string, byte[]>
                {
                    [$"{SourceDir}/first.txt"] = "Some Content"u8.ToArray(),
                },
                rootPath => new CopyRequest
                {
                    Source = new DirectoryInfo(Path.Combine(rootPath, SourceDir)),
                    Destination = new DirectoryInfo(Path.Combine(rootPath, DestinationDir)),
                    Recursive = true,
                    Overwrite = OverwriteMode.Always,
                    CreateDirectories = true,
                    ReplaceContent = false,
                    ReplaceNames = true,
                    DryRun = true,
                    Verbose = true,
                    Replace = Array.Empty<string>().ToLookup(e => e),
                    Encoding = null,
                    ReplaceMode = ReplaceMode.Plain,
                    Include = [],
                    Exclude = [],
                }
            );
        }
    }
}
