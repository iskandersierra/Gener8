using System.Security.AccessControl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
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

public class CopyRequest
{
    public required FileSystemInfo Source { get; init; }
    public required FileSystemInfo Destination { get; init; }

    public required bool Recursive { get; init; }
    public required OverwriteMode Overwrite { get; init; }
    public required bool CreateDirectories { get; init; }
    public required bool DryRun { get; init; }
    public required bool Verbose { get; init; }

    public required IEnumerable<string> Include { get; init; }
    public required IEnumerable<string> Exclude { get; init; }
}

public class CopyResult
{
    public required bool Success { get; init; }
}

public record CopyOperationDescription();

public class CopyService(IAnsiConsole console) : ICopyService
{
    public async Task<CopyResult> ExecuteAsync(
        CopyRequest request,
        CancellationToken cancel = default
    )
    {
        var matcher = new Matcher();

        matcher.AddInclude("**/*");

        foreach (var include in request.Include)
        {
            matcher.AddInclude(include);
        }
        foreach (var exclude in request.Exclude)
        {
            matcher.AddExclude(exclude);
        }

        var ensuredDirectories = new HashSet<string>();

        await Loop(request.Source, request.Destination);

        return new() { Success = false };

        async Task Loop(FileSystemInfo fromInfo, FileSystemInfo toInfo)
        {
            if (fromInfo is FileInfo fromFile)
            {
                if (toInfo is FileInfo toFile)
                {
                    if (matcher.Match(fromFile.FullName).HasMatches)
                    {
                        await CopyFile(fromFile, toFile);
                    }
                }
                else if (toInfo is DirectoryInfo toDir)
                {
                    if (matcher.Match(fromFile.FullName).HasMatches)
                    {
                        toFile = new FileInfo(Path.Combine(toDir.FullName, fromFile.Name));
                        await CopyFile(fromFile, toFile);
                    }
                }
            }
            else if (fromInfo is DirectoryInfo fromDir)
            {
                if (toInfo is FileInfo toFile)
                {
                    console.MarkupInterpolated(
                        $"[red]Cannot copy directory [cyan]{fromDir.FullName}[/] to file [cyan]{toFile.FullName}[/]\n"
                    );
                }
                else if (toInfo is DirectoryInfo toDir)
                {
                    var fileMatches = matcher.Match(fromDir.GetFiles().Select(e => e.FullName));

                    if (fileMatches.HasMatches)
                    {
                        foreach (var match in fileMatches.Files)
                        {
                            fromFile = new FileInfo(match.Path);
                            var toChildFile = new FileInfo(
                                Path.Combine(toDir.FullName, fromFile.Name)
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
                                Path.Combine(toDir.FullName, fromDir.Name)
                            );
                            await Loop(fromDir, toChildDir);
                        }
                    }
                }
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
