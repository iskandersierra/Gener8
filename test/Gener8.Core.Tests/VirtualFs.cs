namespace Gener8.Core.Tests;

/// <summary>
/// A virtual file system for testing.
/// It creates a temporary folder and allows creating folders and files thar are deleted after the test.
/// </summary>
public readonly struct VirtualFs : IAsyncDisposable
{
    private readonly string rootPath;

    public string RootPath => rootPath;

    public VirtualFs(string name)
    {
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), name));
        rootPath = directory.FullName;
        Directory.CreateDirectory(rootPath);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
        Directory.Delete(rootPath, true);
    }
}
