using Spectre.Console.Cli;

namespace Gener8.Commands;

public static class TemplateGenerateCommand
{
    public static IConfigurator<TemplateBranch.Settings> AddTemplateGenerateCommand(
        this IConfigurator<TemplateBranch.Settings> configurator
    )
    {
        configurator.AddCommand<Command>("generate").WithAlias("gen").WithAlias("g");

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
