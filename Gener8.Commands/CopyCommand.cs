using Spectre.Console.Cli;

namespace Gener8.Commands;

public static class CopyCommand
{
    public static IConfigurator AddCopy(this IConfigurator configurator)
    {
        configurator.AddCommand<Command>("copy").WithAlias("cp");

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
