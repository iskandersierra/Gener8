using Gener8;
using Gener8.Commands;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

var services = new ServiceCollection();

services.AddSingleton(AnsiConsole.Console);

var registrar = new CustomTypeRegistrar(services);

var app = new CommandApp<TemplateGenerateCommand.Command>(registrar);

app.Configure(config =>
{
    config.SetApplicationName("gener8");
    config.SetApplicationVersion("0.1");

#if DEBUG
    config.SetExceptionHandler(
        (exception, resolver) =>
        {
            AnsiConsole.WriteException(exception);
            return 1;
        }
    );
#endif

    config.AddCopy().AddTemplate();
});

return await app.RunAsync(args);
