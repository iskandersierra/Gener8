using System;
using Gener8.Core;
using Microsoft.Extensions.FileSystemGlobbing;
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
                WriteSettings(settings);
            }

            var result = await copyService.ExecuteAsync(request);

            return result.Success ? 0 : 1;
        }

        private void WriteSettings(Settings settings)
        {
            var table = new Table();
            table.AddColumn("Argument");
            table.AddColumn("Value");
            table.Border(TableBorder.Simple);

            table.AddRow("Source".AsHeaderMarkup(), settings.Source.AsMarkup());
            table.AddRow("Destination".AsHeaderMarkup(), settings.Destination.AsMarkup());
            table.AddRow("No recursive".AsHeaderMarkup(), settings.NoRecursive.AsMarkup());
            table.AddRow("No overwrite".AsHeaderMarkup(), settings.NoOverwrite.AsMarkup());
            table.AddRow(
                "No create directories".AsHeaderMarkup(),
                settings.NoCreateDirectories.AsMarkup()
            );
            table.AddRow("Dry run".AsHeaderMarkup(), settings.DryRun.AsMarkup());
            table.AddRow("Verbose".AsHeaderMarkup(), settings.Verbose.AsMarkup());

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
            var sourceType = Source.GetPathType();

            switch (sourceType)
            {
                case PathType.File:
                case PathType.Directory:
                    break;
                case PathType.Inaccessible:
                    return ValidationResult.Error($"Source '{Source}' is inaccessible.");
                case PathType.MissingFile:
                    return ValidationResult.Error($"Source '{Source}' is a missing file.");
                case PathType.MissingDirectory:
                    return ValidationResult.Error($"Source '{Source}' is a missing directory.");
                case PathType.Unexpected:
                default:
                    return ValidationResult.Error(
                        $"Source '{Source}' is neither a file nor a directory."
                    );
            }

            if (Destination is not null)
            {
                switch (Destination.GetPathType())
                {
                    case PathType.File:
                        if (sourceType == PathType.Directory)
                        {
                            return ValidationResult.Error(
                                $"Source '{Source}' is a directory, but destination '{Destination}' is a file."
                            );
                        }

                        break;

                    case PathType.Directory:
                        break;
                    case PathType.Inaccessible:
                        return ValidationResult.Error(
                            $"Destination '{Destination}' is inaccessible."
                        );
                    case PathType.MissingDirectory:
                        return ValidationResult.Error(
                            $"Destination '{Destination}' is a missing directory."
                        );
                    case PathType.MissingFile:
                        return ValidationResult.Error(
                            $"Destination '{Destination}' is a missing file."
                        );
                    case PathType.Unexpected:
                    default:
                        return ValidationResult.Error(
                            $"Destination '{Destination}' is neither a file nor a directory."
                        );
                }
            }

            return ValidationResult.Success();
        }
    }

    internal static CopyRequest MapToRequest(this Settings settings)
    {
        var sourceType = settings.Source.GetPathType();
        FileSystemInfoBase source = sourceType switch
        {
            PathType.File => new FileInfoWrapper(new FileInfo(settings.Source)),
            PathType.Directory => new DirectoryInfoWrapper(new DirectoryInfo(settings.Source)),
            _ => throw new InvalidOperationException($"Unexpected source type: {sourceType}"),
        };

        return new CopyRequest
        {
            Source = source,
            Destination = settings.Destination,
            Verbose = settings.Verbose ?? false,
            DryRun = settings.DryRun ?? false,
            CreateDirectories = !(settings.NoCreateDirectories ?? false),
            Overwrite = !(settings.NoOverwrite ?? false),
            Recursive = !(settings.NoRecursive ?? false),
        };
    }
}
