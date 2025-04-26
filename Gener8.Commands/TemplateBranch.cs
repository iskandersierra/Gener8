using Spectre.Console.Cli;

namespace Gener8.Commands;

public static class TemplateBranch
{
    public static IConfigurator AddTemplate(this IConfigurator configurator)
    {
        configurator
            .AddBranch<Settings>(
                "template",
                template =>
                {
                    template
                        .AddTemplateGenerateCommand()
                        .AddTemplateListCommand()
                        .AddTemplateInfoCommand();
                }
            )
            .WithAlias("temp")
            .WithAlias("t");

        return configurator;
    }

    public class Settings : CommandSettings { }
}
