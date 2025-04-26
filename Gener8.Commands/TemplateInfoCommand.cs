using Spectre.Console.Cli;

namespace Gener8.Commands;

public static class TemplateInfoCommand
{
    public static IConfigurator<TemplateBranch.Settings> AddTemplateInfoCommand(
        this IConfigurator<TemplateBranch.Settings> configurator
    )
    {
        configurator.AddCommand<Command>("information").WithAlias("info");

        return configurator;
    }

    public class Command : AsyncCommand<Settings>
    {
        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            await Task.Yield();

            return 0;
        }
    }

    public class Settings : TemplateBranch.Settings { }
}
