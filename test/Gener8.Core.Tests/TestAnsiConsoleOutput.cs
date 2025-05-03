using Spectre.Console;
using System.Text;

namespace Gener8.Core.Tests;

public sealed class TestAnsiConsoleOutput : IAnsiConsoleOutput
{
    public TextWriter Writer { get; }

    public bool IsTerminal => false;

    public int Width => 1000;

    public int Height => 80;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnsiConsoleOutput"/> class.
    /// </summary>
    /// <param name="writer">The output writer.</param>
    public TestAnsiConsoleOutput(TextWriter writer)
    {
        Writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public void SetEncoding(Encoding encoding) { }
}
