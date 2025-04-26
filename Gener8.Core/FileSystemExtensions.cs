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
