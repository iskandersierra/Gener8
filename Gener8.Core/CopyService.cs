using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Spectre.Console;

namespace Gener8.Core;

public interface ICopyService
{
    Task<CopyResult> ExecuteAsync(CopyRequest request, CancellationToken cancel = default);
}

public class CopyRequest
{
    public required FileSystemInfoBase Source { get; init; }
    public string? Destination { get; init; }
    public Matcher? FileMatcher { get; init; }

    public bool Recursive { get; init; }
    public bool Overwrite { get; init; }
    public bool CreateDirectories { get; init; }
    public bool DryRun { get; init; }
    public bool Verbose { get; init; }
}

public class CopyResult
{
    public bool Success { get; set; }
}

public class CopyService(IAnsiConsole console) : ICopyService
{
    public async Task<CopyResult> ExecuteAsync(
        CopyRequest request,
        CancellationToken cancel = default
    )
    {
        await Task.Yield();

        return new() { Success = false };
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
