using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Gener8.Core;

public interface ITemplateService
{
    Task<TemplateResult> ExecuteAsync(TemplateRequest request, CancellationToken cancel = default);
}

public class TemplateRequest
{
    public required FileSystemInfoBase Source { get; init; }

    public bool DryRun { get; init; }
}

public class TemplateResult
{
    public required FileSystemInfoBase Source { get; init; }
}

public static class TemplateServiceExtensions
{
    public static IServiceCollection AddTemplateServices(this IServiceCollection services)
    {
        // services.AddSingleton<ITemplateService, TemplateService>();
        return services;
    }
}
