using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Spectre.Console;

namespace Gener8.Core;

public interface ICopyService
{
    Task<CopyResult> ExecuteAsync(CopyRequest request, CancellationToken cancel = default);
}

public enum OverwriteMode
{
    Never,
    Prompt,
    Always,
}

public enum ReplaceMode
{
    Plain,
    Regex,
}

public class CopyRequest
{
    public required FileSystemInfo Source { get; init; }
    public required FileSystemInfo Destination { get; init; }

    public required bool Recursive { get; init; }
    public required OverwriteMode Overwrite { get; init; }
    public required bool CreateDirectories { get; init; }
    public required bool ReplaceContent { get; init; }
    public required bool ReplaceNames { get; init; }
    public required bool DryRun { get; init; }
    public required bool Verbose { get; init; }

    public required ILookup<string, string> Replace { get; init; }
    public required string? Encoding { get; init; }
    public required ReplaceMode ReplaceMode { get; init; }

    public required IReadOnlyCollection<string> Include { get; init; }
    public required IReadOnlyCollection<string> Exclude { get; init; }
}

public record CopyResult(bool Success);

public record CopyOperationDescription();

public class CopyService(IAnsiConsole console) : ICopyService
{
    public async Task<CopyResult> ExecuteAsync(
        CopyRequest request,
        CancellationToken cancel = default
    )
    {
        var matcher = new Matcher();

        if (request.Include.Count == 0)
        {
            matcher.AddInclude("**/*");
        }
        else
        {
            foreach (var include in request.Include)
            {
                matcher.AddInclude(include);
            }
        }

        foreach (var exclude in request.Exclude)
        {
            matcher.AddExclude(exclude);
        }

        var ensuredDirectories = new HashSet<string>();

        return await Loop(request.Source, request.Destination);

        async Task<CopyResult> Loop(FileSystemInfo fromInfo, FileSystemInfo toInfo)
        {
            switch (fromInfo, toInfo)
            {
                case (FileInfo fromFile, FileInfo toFile):
                {
                    await CopyFile(fromFile, toFile);
                    return new CopyResult(true);
                }

                case (FileInfo fromFile, DirectoryInfo toDir):
                {
                    var toFile = new FileInfo(
                        Path.Combine(toDir.FullName, ReplaceName(fromFile.Name))
                    );
                    await CopyFile(fromFile, toFile);
                    return new CopyResult(true);
                }

                case (DirectoryInfo fromDir, FileInfo toFile):
                {
                    console.MarkupInterpolated(
                        $"[red]Cannot copy directory [cyan]{fromDir.FullName}[/] to file [cyan]{toFile.FullName}[/][/]\n"
                    );
                    return new CopyResult(false);
                }

                case (DirectoryInfo fromDir, DirectoryInfo toDir):
                {
                    var fileMatches = matcher.Match(fromDir.GetFiles().Select(e => e.FullName));

                    if (fileMatches.HasMatches)
                    {
                        foreach (var match in fileMatches.Files)
                        {
                            var fromFile = new FileInfo(match.Path);
                            var toChildFile = new FileInfo(
                                Path.Combine(toDir.FullName, ReplaceName(fromFile.Name))
                            );
                            await Loop(fromFile, toChildFile);
                        }
                    }

                    if (
                        request.Recursive
                        && matcher.Match(fromDir.GetDirectories().Select(e => e.FullName))
                            is { HasMatches: true } dirMatches
                    )
                    {
                        foreach (var matches in dirMatches.Files)
                        {
                            fromDir = new DirectoryInfo(matches.Path);
                            var toChildDir = new DirectoryInfo(
                                Path.Combine(toDir.FullName, ReplaceName(fromDir.Name))
                            );
                            await Loop(fromDir, toChildDir);
                        }
                    }

                    return new CopyResult(true);
                }

                default:
                    console.MarkupInterpolated(
                        $"[red]Cannot copy [cyan]{fromInfo.FullName}[/] to [cyan]{toInfo.FullName}[/][/]\n"
                    );

                    return new CopyResult(false);
            }
        }

        async Task CopyFile(FileInfo fromFile, FileInfo toFile)
        {
            if (toFile.Exists)
            {
                switch (request.Overwrite)
                {
                    case OverwriteMode.Never:
                        if (request.Verbose)
                        {
                            console.MarkupInterpolated(
                                $"[yellow]Skipping [cyan]{toFile.FullName}[/] (overwrite never)\n"
                            );
                        }
                        return;

                    case OverwriteMode.Prompt:
                    {
                        var overwrite = await console.ConfirmAsync(
                            $"Overwrite [cyan]{toFile.FullName}[/]?",
                            cancellationToken: cancel
                        );

                        if (!overwrite)
                        {
                            if (request.Verbose)
                            {
                                console.MarkupInterpolated(
                                    $"[yellow]Skipping [cyan]{toFile.FullName}[/] (overwrite prompt, skipped)\n"
                                );
                            }

                            return;
                        }

                        break;
                    }

                    case OverwriteMode.Always:
                        break;
                }
            }

            if (toFile.Directory is not null && !await EnsureDirectory(toFile.Directory))
                return;

            if (request.Verbose || request.DryRun)
            {
                console.MarkupInterpolated(
                    $"Copying [green]{fromFile.FullName}[/] to [cyan]{toFile.FullName}[/]\n"
                );
            }

            if (!request.DryRun)
            {
                File.Copy(fromFile.FullName, toFile.FullName);
            }

            await Task.Yield();
        }

        async Task<bool> EnsureDirectory(DirectoryInfo dir)
        {
            if (dir.Exists || !ensuredDirectories.Add(dir.FullName))
                return true;

            if (!request.CreateDirectories)
            {
                if (request.Verbose || request.DryRun)
                {
                    console.MarkupInterpolated(
                        $"Directory [cyan]{dir.FullName}[/] does not exist.\n"
                    );
                }

                return false;
            }

            if (request.Verbose || request.DryRun)
            {
                console.MarkupInterpolated($"Creating directory [cyan]{dir.FullName}[/]\n");
            }

            if (!request.DryRun)
            {
                dir.Create();
            }

            await Task.Yield();

            return true;
        }

        string ReplaceName(string name)
        {
            if (!request.ReplaceNames)
            {
                return name;
            }

            foreach (var group in request.Replace)
            {
                var key = group.Key;
                var value = group.First();

                switch (request.ReplaceMode)
                {
                    case ReplaceMode.Plain:
                        name = name.Replace(key, value);
                        break;
                    case ReplaceMode.Regex:
                        name = Regex.Replace(name, key, value);
                        break;
                }
            }

            return name;
        }
    }
}

public static class CopyServiceExtensions
{
    public static IServiceCollection AddCopyServices(this IServiceCollection services)
    {
        services.AddSingleton<ICopyService, CopyService>();
        return services;
    }
}
