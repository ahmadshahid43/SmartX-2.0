using Microsoft.Extensions.Hosting;

namespace OmniBusiness.Infrastructure.Persistence;

internal static class WritableStoragePathResolver
{
    public static string ResolveWritableDirectory(IHostEnvironment environment, params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var fullPath = ResolvePath(environment, candidate);
            if (TryEnsureWritableDirectory(fullPath))
            {
                return fullPath;
            }
        }

        var emergencyPath = Path.Combine(environment.ContentRootPath, ".omnibusiness-runtime");
        Directory.CreateDirectory(emergencyPath);
        return emergencyPath;
    }

    public static string ResolveWritableFilePath(
        IHostEnvironment environment,
        string configuredPath,
        string fallbackRelativePath)
    {
        var preferredPath = ResolvePath(environment, configuredPath);
        var preferredDirectory = Path.GetDirectoryName(preferredPath);

        if (!string.IsNullOrWhiteSpace(preferredDirectory) && TryEnsureWritableDirectory(preferredDirectory))
        {
            return preferredPath;
        }

        return ResolvePath(environment, fallbackRelativePath);
    }

    private static bool TryEnsureWritableDirectory(string directoryPath)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);
            var probePath = Path.Combine(directoryPath, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolvePath(IHostEnvironment environment, string configuredPath)
    {
        var expanded = Environment.ExpandEnvironmentVariables(configuredPath);

        return Path.IsPathRooted(expanded)
            ? expanded
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, expanded));
    }
}
