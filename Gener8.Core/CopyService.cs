using System.Text;
using System.Text.RegularExpressions;
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

public enum ReplaceMode
{
    Plain,
    Regex,
}

public class CopyRequest
{
    public required FileSystemInfo Source { get; init; }
    public required FileSystemInfo Destination { get; init; }

    public bool Recursive { get; init; } = true;
    public OverwriteMode Overwrite { get; init; } = OverwriteMode.Always;
    public bool CreateDirectories { get; init; } = true;
    public bool ReplaceContent { get; init; } = true;
    public bool ReplaceNames { get; init; } = true;
    public bool DryRun { get; init; } = false;
    public bool Verbose { get; init; } = false;
    public bool IgnoreCase { get; init; } = false;

    public ILookup<string, string> Replace { get; init; } = Array.Empty<string>().ToLookup(e => e);
    public string? Encoding { get; init; } = null;
    public ReplaceMode ReplaceMode { get; init; } = ReplaceMode.Plain;

    public IReadOnlyCollection<string> Include { get; init; } = [];
    public IReadOnlyCollection<string> Exclude { get; init; } = [];
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
                    return new CopyResult(await CopyFile(fromFile, toFile));
                }

                case (FileInfo fromFile, DirectoryInfo toDir):
                {
                    var toFile = new FileInfo(
                        Path.Combine(toDir.FullName, ReplaceName(fromFile.Name))
                    );

                    return new CopyResult(await CopyFile(fromFile, toFile));
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
                    if (request.Recursive)
                    {
                        var matches = matcher.Execute(new DirectoryInfoWrapper(fromDir));
                        var success = true;

                        foreach (var match in matches.Files)
                        {
                            var fromChildFile = new FileInfo(
                                Path.Combine(fromDir.FullName, match.Path)
                            );

                            var toChildFile = new FileInfo(
                                Path.Combine(toDir.FullName, ReplaceName(match.Path))
                            );

                            var result = await Loop(fromChildFile, toChildFile);
                            success &= result.Success;
                        }

                        return new CopyResult(success);
                    }
                    else
                    {
                        var success = true;

                        if (
                            fromDir.GetFiles().Select(e => e.FullName).ToArray() is var allFiles
                            && matcher.Match(fromDir.FullName, allFiles)
                                is { HasMatches: true } fileMatches
                        )
                        {
                            foreach (var match in fileMatches.Files)
                            {
                                var fromChildFile = new FileInfo(
                                    Path.Combine(fromDir.FullName, match.Path)
                                );

                                var toChildFile = new FileInfo(
                                    Path.Combine(toDir.FullName, ReplaceName(fromChildFile.Name))
                                );

                                var result = await Loop(fromChildFile, toChildFile);
                                success &= result.Success;
                            }
                        }

                        return new CopyResult(success);
                    }
                }

                default:
                    console.MarkupInterpolated(
                        $"[red]Cannot copy [cyan]{fromInfo.FullName}[/] to [cyan]{toInfo.FullName}[/][/]\n"
                    );

                    return new CopyResult(false);
            }
        }

        async Task<bool> CopyFile(FileInfo fromFile, FileInfo toFile)
        {
            await Task.Yield();

            var action = "Copying";

            if (toFile.Exists)
            {
                action = "Overwriting";

                switch (request.Overwrite)
                {
                    case OverwriteMode.Never:
                        if (request.Verbose)
                        {
                            console.MarkupInterpolated(
                                $"[yellow]Skipping [cyan]{toFile.FullName}[/] (overwrite never)[/]\n"
                            );
                        }
                        return true;

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
                                    $"[yellow]Skipping [cyan]{toFile.FullName}[/] (overwrite prompt, skipped)[/]\n"
                                );
                            }

                            return true;
                        }

                        break;
                    }

                    case OverwriteMode.Always:
                        break;
                }
            }

            if (toFile.Directory is not null && !await EnsureDirectory(toFile.Directory))
                return false;

            if (request.Verbose || request.DryRun)
            {
                console.MarkupInterpolated(
                    $"{action} [green]{fromFile.FullName}[/] to [cyan]{toFile.FullName}[/]\n"
                );
            }

            if (request.DryRun)
                return true;

            try
            {
                File.Copy(fromFile.FullName, toFile.FullName, toFile.Exists);

                if (request is { ReplaceContent: true, Replace.Count: > 0 })
                {
                    return await ReplaceContent(toFile);
                }

                return true;
            }
            catch (Exception e)
            {
                console.MarkupInterpolated(
                    $"[red]Error copying [cyan]{fromFile.FullName}[/] to [cyan]{toFile.FullName}[/]: {e.Message}[/]\n"
                );

                return false;
            }
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

        StringComparison GetStringComparison()
        {
            return request.IgnoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
        }

        RegexOptions GetRegexOptions()
        {
            return request.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
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
                        name = name.Replace(key, value, GetStringComparison());
                        break;
                    case ReplaceMode.Regex:
                        name = Regex.Replace(name, key, value, GetRegexOptions());
                        break;
                }
            }

            return name;
        }

        async Task<bool> ReplaceContent(FileInfo toFile)
        {
            Encoding? suggestedEncoding;

            try
            {
                suggestedEncoding = request.Encoding is not null
                    ? Encoding.GetEncoding(request.Encoding)
                    : null;
            }
            catch (Exception e)
            {
                console.MarkupInterpolated(
                    $"[red]Error getting encoding [cyan]{request.Encoding}[/]: {e.Message}\n"
                );

                return false;
            }

            try
            {
                Encoding actualEncoding;
                string content;

                await using (
                    var stream = File.Open(
                        toFile.FullName,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read
                    )
                )
                {
                    using var reader = suggestedEncoding is not null
                        ? new StreamReader(stream, suggestedEncoding, true)
                        : new StreamReader(stream, true);

                    actualEncoding = reader.CurrentEncoding;
                    content = await reader.ReadToEndAsync(cancel);
                }

                foreach (var group in request.Replace)
                {
                    var key = group.Key;
                    var value = group.First();

                    switch (request.ReplaceMode)
                    {
                        case ReplaceMode.Plain:
                            content = content.Replace(key, value, GetStringComparison());
                            break;

                        case ReplaceMode.Regex:
                            content = Regex.Replace(content, key, value, GetRegexOptions());
                            break;
                    }
                }

                await File.WriteAllTextAsync(toFile.FullName, content, actualEncoding, cancel);

                return true;
            }
            catch (Exception e)
            {
                console.MarkupInterpolated(
                    $"[red]Error replacing content in [cyan]{toFile.FullName}[/]: {e.Message}\n"
                );

                return false;
            }
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
