using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Gener8.Core;

public static class FileSystemExtensions
{
    /// <summary>
    /// From: https://stackoverflow.com/questions/1395205/better-way-to-check-if-a-path-is-a-file-or-a-directory#79350289
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static PathType GetPathType(this string path)
    {
        try
        {
            const FileAttributes directoryMask =
                FileAttributes.Device | FileAttributes.Directory | FileAttributes.ReparsePoint;

            var attributes = File.GetAttributes(path);

            if ((attributes & directoryMask) == 0)
            {
                return PathType.File;
            }

            if (attributes.HasFlag(FileAttributes.Directory))
            {
                return PathType.Directory;
            }

            return PathType.Unexpected;
        }
        catch (FileNotFoundException)
        {
            return PathType.MissingFile;
        }
        catch (DirectoryNotFoundException)
        {
            return PathType.MissingDirectory;
        }
        catch
        {
            return PathType.Inaccessible;
        }
    }

    public static FileSystemInfoBase GetFileSystemInfo(
        this string path,
        PathType? missingType = null
    )
    {
        return GetFileSystemInfoAndType(path, missingType) switch
        {
            ({ } info, _) => info,

            var (_, t) => throw new InvalidOperationException($"Unexpected type {t} for '{path}'"),
        };
    }

    public static (FileSystemInfoBase? Info, PathType Type) GetFileSystemInfoAndType(
        this string path,
        PathType? missingType = null
    )
    {
        var pathType = path.GetPathType();

        return pathType switch
        {
            PathType.File => (new FileInfoWrapper(new FileInfo(path)), pathType),

            PathType.Directory => (new DirectoryInfoWrapper(new DirectoryInfo(path)), pathType),

            PathType.MissingFile or PathType.MissingDirectory => missingType switch
            {
                PathType.File => (new FileInfoWrapper(new FileInfo(path)), PathType.MissingFile),
                PathType.Directory => (
                    new DirectoryInfoWrapper(new DirectoryInfo(path)),
                    PathType.MissingDirectory
                ),
                _ => (null, pathType),
            },

            _ => (null, pathType),
        };
    }
}

public enum PathType
{
    Unexpected = 0,
    Directory,
    File,
    MissingDirectory,
    MissingFile,
    Inaccessible,
}
