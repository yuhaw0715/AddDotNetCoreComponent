namespace DotNetScaffoldStudio.IntegrationTests;

[CollectionDefinition("CLI isolation", DisableParallelization = true)]
public sealed class CliIsolationCollection;

internal sealed class CliTestIsolation : IDisposable
{
    private CliTestIsolation(string rootPath)
    {
        RootPath = rootPath;
        DotNetCliHome = Path.Combine(rootPath, "dotnet-cli-home");
        NuGetPackages = Path.Combine(rootPath, "nuget-packages");
        NuGetHttpCache = Path.Combine(rootPath, "nuget-http-cache");
        Directory.CreateDirectory(DotNetCliHome);
        Directory.CreateDirectory(NuGetPackages);
        Directory.CreateDirectory(NuGetHttpCache);
    }

    public string RootPath { get; }
    public string DotNetCliHome { get; }
    public string NuGetPackages { get; }
    public string NuGetHttpCache { get; }

    public IReadOnlyDictionary<string, string> CommandEnvironment =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_CLI_HOME"] = DotNetCliHome,
            ["NUGET_PACKAGES"] = NuGetPackages,
            ["NUGET_HTTP_CACHE_PATH"] = NuGetHttpCache,
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1"
        };

    public static CliTestIsolation Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            $"dotnet-scaffold-studio-cli-{name}-{Guid.NewGuid():N}");
        return new CliTestIsolation(rootPath);
    }

    public static IReadOnlyList<CliPathState> CaptureUserProfileState()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var paths = new[]
        {
            Path.Combine(profile, ".dotnet"),
            Path.Combine(profile, ".nuget"),
            Path.Combine(profile, ".config", "dotnet-tools.json")
        };
        return paths.Select(CliPathState.Capture).ToArray();
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}

internal sealed record CliPathState(string Path, bool Exists, DateTime LastWriteTimeUtc)
{
    public static CliPathState Capture(string path) =>
        new(
            path,
            File.Exists(path) || Directory.Exists(path),
            GetLastWriteTimeUtc(path));

    private static DateTime GetLastWriteTimeUtc(string path)
    {
        try
        {
            return File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : Directory.Exists(path)
                    ? Directory.GetLastWriteTimeUtc(path)
                    : DateTime.MinValue;
        }
        catch (IOException)
        {
            return DateTime.MinValue;
        }
        catch (UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }
}
