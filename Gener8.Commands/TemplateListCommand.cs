using Spectre.Console.Cli;

namespace Gener8.Commands;

public static class TemplateListCommand
{
    public static IConfigurator<TemplateBranch.Settings> AddTemplateListCommand(
        this IConfigurator<TemplateBranch.Settings> configurator
    )
    {
        configurator.AddCommand<Command>("list").WithAlias("ls").WithAlias("l");

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
