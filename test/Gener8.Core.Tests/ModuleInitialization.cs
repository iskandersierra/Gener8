using System.Runtime.CompilerServices;
using DiffEngine;

internal class ModuleInitialization
{
    [ModuleInitializer]
    internal static void Init()
    {
        DiffTools.UseOrder(DiffTool.VisualStudioCode);

        VerifierSettings.DontIgnoreEmptyCollections();
    }
}
