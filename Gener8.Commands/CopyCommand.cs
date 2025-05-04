using System.Diagnostics;
using System.Xml.Linq;
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
            table.AddRow("Include".AsHeaderMarkup(), string.Join(", ", request.Include).AsMarkup());
            table.AddRow("Exclude".AsHeaderMarkup(), string.Join(", ", request.Exclude).AsMarkup());

            table.AddRow("Recursive".AsHeaderMarkup(), request.Recursive.AsMarkup());
            table.AddRow("Ignore case".AsHeaderMarkup(), request.IgnoreCase.AsMarkup());
            table.AddRow("Replace names".AsHeaderMarkup(), request.ReplaceNames.AsMarkup());
            table.AddRow("Replace content".AsHeaderMarkup(), request.ReplaceContent.AsMarkup());
            table.AddRow("Overwrite".AsHeaderMarkup(), request.Overwrite.ToString().AsMarkup());
            table.AddRow(
                "Replace mode".AsHeaderMarkup(),
                request.ReplaceMode.ToString().AsMarkup()
            );
            table.AddRow(
                "Create directories".AsHeaderMarkup(),
                request.CreateDirectories.AsMarkup()
            );
            table.AddRow("Encoding".AsHeaderMarkup(), request.Encoding.AsMarkup());
            table.AddRow("Dry run".AsHeaderMarkup(), request.DryRun.AsMarkup());
            table.AddRow("Verbose".AsHeaderMarkup(), request.Verbose.AsMarkup());
            foreach (var group in request.Replace)
            {
                table.AddRow(
                    "Replace".AsHeaderMarkup(),
                    $"{group.Key} => {group.First()}".AsMarkup()
                );
            }

            console.Write(table);
        }
    }

    public class Settings : TemplateBranch.Settings
    {
        [CommandArgument(0, "<source>")]
        public required string Source { get; set; }

        [CommandArgument(1, "[destination]")]
        public string? Destination { get; set; }

        [CommandOption("--no-recursive")]
        public bool? NoRecursive { get; set; }

        [CommandOption("-p|--prompt-overwrite")]
        public bool? PromptOverwrite { get; set; }

        [CommandOption("--no-overwrite")]
        public bool? NoOverwrite { get; set; }

        [CommandOption("--no-create-directories")]
        public bool? NoCreateDirectories { get; set; }

        [CommandOption("--no-replace-content")]
        public bool? NoReplaceContent { get; set; }

        [CommandOption("--no-replace-names")]
        public bool? NoReplaceNames { get; set; }

        [CommandOption("--dry-run")]
        public bool? DryRun { get; set; }

        [CommandOption("--verbose")]
        public bool? Verbose { get; set; }

        [CommandOption("--ignore-case")]
        public bool? IgnoreCase { get; set; }

        [CommandOption("-i|--include")]
        public string[] Include { get; set; } = [];

        [CommandOption("-x|--exclude")]
        public string[] Exclude { get; set; } = [];

        [CommandOption("--encoding")]
        public string? Encoding { get; set; }

        [CommandOption("--regex")]
        public bool? Regex { get; set; }

        [CommandOption("-r|--replace")]
        public ILookup<string, string>? Replace { get; set; }

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

            message = ValidateEncoding();

            if (message is not null)
                return ValidationResult.Error(message);

            message = ValidateReplace();

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

        private string? ValidateEncoding()
        {
            if (Encoding is { } encoding)
            {
                if (int.TryParse(encoding, out var codepage))
                {
                    try
                    {
                        _ = System.Text.Encoding.GetEncoding(codepage);
                        return null;
                    }
                    catch (Exception e)
                    {
                        return e.Message;
                    }
                }

                try
                {
                    _ = System.Text.Encoding.GetEncoding(encoding);
                }
                catch (Exception e)
                {
                    return e.Message;
                }
            }

            return null;
        }

        private string? ValidateReplace()
        {
            if (Replace is null)
            {
                Replace = Array.Empty<string>().ToLookup(e => e);
            }
            else
            {
                foreach (var group in Replace)
                {
                    if (group.Count() > 1)
                    {
                        return $"Replace key '{group.Key}' has more than one replacement value.";
                    }
                }
            }

            return null;
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

            var replaceMode = Regex ?? false ? ReplaceMode.Regex : ReplaceMode.Plain;

            return new CopyRequest
            {
                Source = sourceInfo,
                Destination = destinationInfo,
                Verbose = Verbose ?? false,
                DryRun = DryRun ?? false,
                CreateDirectories = !(NoCreateDirectories ?? false),
                Overwrite = overwrite,
                Recursive = !(NoRecursive ?? false),
                ReplaceContent = !(NoReplaceContent ?? false),
                ReplaceNames = !(NoReplaceNames ?? false),
                IgnoreCase = IgnoreCase ?? false,
                Encoding = Encoding,
                Replace = Replace!,
                ReplaceMode = replaceMode,
                Include = Include.AsReadOnly(),
                Exclude = Exclude.AsReadOnly(),
            };
        }
    }
}
