using System.Diagnostics;
using System.Text.Json;

namespace DotNetScaffoldStudio.CommandProbe;

public static class ProbeMarker;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args is ["mixed"])
        {
            for (var index = 0; index < 4096; index++)
            {
                Console.Out.WriteLine($"OUT {index:D4} {new string('o', 192)}");
                Console.Error.WriteLine($"ERR {index:D4} {new string('e', 192)}");
            }

            return 23;
        }

        if (args is ["secret", var secret])
        {
            Console.Out.WriteLine($"stdout={secret}");
            Console.Error.WriteLine($"stderr={secret}");
            return 0;
        }

        if (args is ["new", "tool-manifest"])
        {
            await CreateToolManifestAsync();
            return 0;
        }

        if (args is ["tool", "install" or "update", var packageId, "--local", "--version", var version])
        {
            await InstallLocalToolAsync(packageId, version);
            return 0;
        }

        if (args is ["wait"])
        {
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }

        if (args is ["partial-tree-wait"])
        {
            await File.WriteAllTextAsync("PartiallyGenerated.cs", "partial output before cancellation");
            Console.Out.WriteLine("PARTIAL_OUTPUT_WRITTEN");

            var childPidPath = Path.Combine(Environment.CurrentDirectory, "child.pid");
            var childStartInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? throw new InvalidOperationException("找不到目前的 dotnet host。"),
                WorkingDirectory = Environment.CurrentDirectory,
                UseShellExecute = false
            };
            childStartInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
            childStartInfo.ArgumentList.Add("child-wait");
            childStartInfo.ArgumentList.Add(childPidPath);
            _ = Process.Start(childStartInfo)
                ?? throw new InvalidOperationException("無法啟動測試用子程序。");

            await WaitForFileAsync(childPidPath);
            var childPid = await File.ReadAllTextAsync(childPidPath);
            Console.Out.WriteLine($"CHILD_STARTED {childPid}");
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }

        if (args is ["child-wait", var childPidFile])
        {
            await File.WriteAllTextAsync(childPidFile, Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await Task.Delay(Timeout.InfiniteTimeSpan);
        }

        return 0;
    }

    private static async Task CreateToolManifestAsync()
    {
        var directory = Path.Combine(Environment.CurrentDirectory, ".config");
        Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, "dotnet-tools.json");
        if (!File.Exists(manifestPath))
        {
            await File.WriteAllTextAsync(
                manifestPath,
                "{\"version\":1,\"isRoot\":true,\"tools\":{}}");
        }
    }

    private static async Task InstallLocalToolAsync(string packageId, string version)
    {
        var directory = Path.Combine(Environment.CurrentDirectory, ".config");
        Directory.CreateDirectory(directory);
        var manifestPath = Path.Combine(directory, "dotnet-tools.json");
        var manifest = File.Exists(manifestPath)
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(await File.ReadAllTextAsync(manifestPath))
            : null;
        var tools = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (manifest?.TryGetValue("tools", out var existingTools) == true &&
            existingTools is JsonElement toolsElement &&
            toolsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in toolsElement.EnumerateObject())
            {
                tools[property.Name] = property.Value;
            }
        }

        tools[packageId] = new
        {
            version,
            commands = new[] { packageId }
        };
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(new
            {
                version = 1,
                isRoot = true,
                tools
            }));

        var executableDirectory = Path.Combine(directory, "dotnet-tools");
        Directory.CreateDirectory(executableDirectory);
        var executablePath = Path.Combine(executableDirectory, packageId);
        await File.WriteAllTextAsync(executablePath, "#!/usr/bin/env fixture\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                executablePath,
                UnixFileMode.UserRead |
                UnixFileMode.UserWrite |
                UnixFileMode.UserExecute);
        }
    }

    private static async Task WaitForFileAsync(string path)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!File.Exists(path))
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
