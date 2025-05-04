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
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));
        var tempPath = Path.GetTempPath();
        var path = Path.Combine(tempPath, name);
        if (Path.GetRelativePath(tempPath, path).Contains(".."))
            throw new ArgumentException("Name cannot contain path separators.", nameof(name));
        if (Directory.Exists(path))
            Directory.Delete(path, true);
        rootPath = Directory.CreateDirectory(path).FullName;
        Directory.CreateDirectory(rootPath);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.CompletedTask;
        Directory.Delete(rootPath, true);
    }
}
