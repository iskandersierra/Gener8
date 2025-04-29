using System.Diagnostics;
using CommunityToolkit.Diagnostics;
using Gener8.Core;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Gener8.Commands;

public static class CopyCommand
{
    public static IConfigurator AddCopy(this IConfigurator configurator)
    {
        configurator.AddCommand<Command>("copy").WithAlias("cp");

        return configurator;
    }

    public class Command(ICopyService copyService, IAnsiConsole console) : AsyncCommand<Settings>
    {
        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var request = settings.MapToRequest();

            if (request.DryRun)
            {
                console.MarkupLine("[yellow]DRY RUN:[/] no changes will be made.");
            }

            if (request.Verbose)
            {
                WriteSettings(request);
            }

            var result = await copyService.ExecuteAsync(request);

            return result.Success ? 0 : 1;
        }

        private void WriteSettings(CopyRequest request)
        {
            var table = new Table();
            table.AddColumn("Argument");
            table.AddColumn("Value");
            table.Border(TableBorder.Simple);

            table.AddRow("Source".AsHeaderMarkup(), request.Source.FullName.AsMarkup());
            table.AddRow("Destination".AsHeaderMarkup(), request.Destination.FullName.AsMarkup());

            table.AddRow("Recursive".AsHeaderMarkup(), request.Recursive.AsMarkup());
            table.AddRow("Overwrite".AsHeaderMarkup(), request.Overwrite.ToString().AsMarkup());
            table.AddRow(
                "Create directories".AsHeaderMarkup(),
                request.CreateDirectories.AsMarkup()
            );
            table.AddRow("Dry run".AsHeaderMarkup(), request.DryRun.AsMarkup());
            table.AddRow("Verbose".AsHeaderMarkup(), request.Verbose.AsMarkup());

            console.Write(table);
        }
    }

    public class Settings : TemplateBranch.Settings
    {
        [CommandArgument(0, "<source>")]
        public required string Source { get; init; }

        [CommandArgument(1, "[destination]")]
        public string? Destination { get; init; }

        [CommandOption("--no-recursive")]
        public bool? NoRecursive { get; init; }

        [CommandOption("-p|--prompt-overwrite")]
        public bool? PromptOverwrite { get; init; }

        [CommandOption("--no-overwrite")]
        public bool? NoOverwrite { get; init; }

        [CommandOption("--no-create-directories")]
        public bool? NoCreateDirectories { get; init; }

        [CommandOption("--dry-run")]
        public bool? DryRun { get; init; }

        [CommandOption("--verbose")]
        public bool? Verbose { get; init; }

        public override ValidationResult Validate()
        {
            var (sourceType, message) = ValidateSource();

            if (message is not null)
                return ValidationResult.Error(message);

            message = ValidateDestination(sourceType);

            if (message is not null)
                return ValidationResult.Error(message);

            message = ValidateOverwrite();

            if (message is not null)
                return ValidationResult.Error(message);

            return ValidationResult.Success();
        }

        private (PathType sourceType, string? errorMessage) ValidateSource()
        {
            return Source.GetFileSystemInfoAndType() switch
            {
                (not null, var t and (PathType.File or PathType.Directory)) => (t, null),

                (not null, PathType.MissingFile) => (
                    PathType.MissingFile,
                    $"Source '{Source}' is a missing file."
                ),

                (not null, PathType.MissingDirectory) => (
                    PathType.MissingDirectory,
                    $"Source '{Source}' is a missing directory."
                ),

                (null, PathType.Inaccessible) => (
                    PathType.Inaccessible,
                    $"Source '{Source}' is inaccessible."
                ),

                _ => (PathType.Unexpected, $"Source '{Source}' is neither a file nor a directory."),
            };
        }

        private string? ValidateDestination(PathType sourceType)
        {
            return Destination?.GetFileSystemInfoAndType(sourceType) switch
            {
                (not null, PathType.Directory or PathType.MissingDirectory) => null,

                (not null, PathType.File or PathType.MissingFile)
                    when sourceType == PathType.Directory =>
                    $"Source '{Source}' is a directory but destination '{Destination}' is a file.",

                (not null, PathType.File or PathType.MissingFile) => null,

                (null, PathType.Inaccessible) => $"Destination '{Destination}' is inaccessible.",

                _ => $"Destination '{Destination}' is neither a file nor a directory.",
            };
        }

        private string? ValidateOverwrite()
        {
            return (PromptOverwrite, NoOverwrite) switch
            {
                (true, true) => "Cannot use both --prompt-overwrite and --no-overwrite.",
                _ => null,
            };
        }

        internal CopyRequest MapToRequest()
        {
            var (sourceInfo, sourceType) = Source.GetFileSystemInfoAndType();
            Guard.IsNotNull(sourceInfo);
            Guard.IsTrue(sourceType is PathType.File or PathType.Directory);

            var (destinationInfo, destinationType) = (
                Destination ?? Source
            ).GetFileSystemInfoAndType(sourceType);
            Guard.IsNotNull(destinationInfo);
            Guard.IsTrue(
                destinationType
                    is PathType.File
                        or PathType.Directory
                        or PathType.MissingFile
                        or PathType.MissingDirectory
            );

            var overwrite = (PromptOverwrite, NoOverwrite) switch
            {
                (true, _) => OverwriteMode.Prompt,
                (_, true) => OverwriteMode.Never,
                _ => OverwriteMode.Always,
            };

            return new CopyRequest
            {
                Source = sourceInfo,
                Destination = destinationInfo,
                Verbose = Verbose ?? false,
                DryRun = DryRun ?? false,
                CreateDirectories = !(NoCreateDirectories ?? false),
                Overwrite = overwrite,
                Recursive = !(NoRecursive ?? false),
            };
        }
    }
}
