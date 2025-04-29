using System.Security.AccessControl;
using Microsoft.Extensions.DependencyInjection;
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
    public required FileSystemInfoBase Source { get; init; }
    public required FileSystemInfoBase Destination { get; init; }

    public required bool Recursive { get; init; }
    public required OverwriteMode Overwrite { get; init; }
    public required bool CreateDirectories { get; init; }
    public required bool DryRun { get; init; }
    public required bool Verbose { get; init; }
}

public class CopyResult
{
    public bool Success { get; set; }
}

public record CopyOperationDescription();

public class CopyService(IAnsiConsole console) : ICopyService
{
    public async Task<CopyResult> ExecuteAsync(
        CopyRequest request,
        CancellationToken cancel = default
    )
    {
        await Loop(request.Source, request.Destination);

        return new() { Success = false };

        async Task Loop(FileSystemInfoBase fromInfo, FileSystemInfoBase toInfo)
        {
            if (fromInfo is FileInfoBase fromFile)
            {
                if (toInfo is FileInfoBase toFile)
                {
                    await CopyFile(fromFile, toFile);
                }
            }
        }

        async Task CopyFile(FileInfoBase fromFile, FileInfoBase toFile)
        {
            if (request.Verbose || request.DryRun)
            {
                console.MarkupInterpolated(
                    $"Copying [green]{fromFile.FullName}[/] to [cyan]{toFile.FullName}[/]\n"
                );
            }

            await Task.Yield();
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
