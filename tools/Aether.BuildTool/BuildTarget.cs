using System.Runtime.InteropServices;

namespace Aether.BuildTool;

internal enum TargetPlatform
{
    Windows,
    Linux,
    MacOS
}

internal enum TargetArchitecture
{
    X64,
    Arm64
}

internal enum BuildConfiguration
{
    Debug,
    Release
}

internal sealed record BuildTarget(
    TargetPlatform Platform,
    TargetArchitecture Architecture,
    BuildConfiguration Configuration)
{
    public string RuntimeIdentifier => (Platform, Architecture) switch
    {
        (TargetPlatform.Windows, TargetArchitecture.X64) => "win-x64",
        (TargetPlatform.Windows, TargetArchitecture.Arm64) => "win-arm64",
        (TargetPlatform.Linux, TargetArchitecture.X64) => "linux-x64",
        (TargetPlatform.Linux, TargetArchitecture.Arm64) => "linux-arm64",
        (TargetPlatform.MacOS, TargetArchitecture.X64) => "osx-x64",
        (TargetPlatform.MacOS, TargetArchitecture.Arm64) => "osx-arm64",
        _ => throw new BuildToolException("Unsupported target platform and architecture combination.")
    };

    public static BuildTarget From(CommandArguments arguments)
    {
        var hostPlatform = GetHostPlatform();
        var hostArchitecture = RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => TargetArchitecture.X64,
            System.Runtime.InteropServices.Architecture.Arm64 => TargetArchitecture.Arm64,
            _ => throw new BuildToolException($"Unsupported host architecture '{RuntimeInformation.ProcessArchitecture}'.")
        };

        return new BuildTarget(
            ParsePlatform(arguments.GetOrDefault("platform", hostPlatform.ToString())),
            ParseArchitecture(arguments.GetOrDefault("architecture", hostArchitecture.ToString())),
            ParseConfiguration(arguments.GetOrDefault("configuration", nameof(BuildConfiguration.Debug))));
    }

    public static TargetPlatform GetHostPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return TargetPlatform.Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return TargetPlatform.Linux;
        }

        if (OperatingSystem.IsMacOS())
        {
            return TargetPlatform.MacOS;
        }

        throw new BuildToolException($"Unsupported host platform '{RuntimeInformation.OSDescription}'.");
    }

    private static TargetPlatform ParsePlatform(string value) => value.ToLowerInvariant() switch
    {
        "windows" or "win" => TargetPlatform.Windows,
        "linux" => TargetPlatform.Linux,
        "macos" or "osx" or "mac" => TargetPlatform.MacOS,
        _ => throw new BuildToolException($"Unsupported platform '{value}'.")
    };

    private static TargetArchitecture ParseArchitecture(string value) => value.ToLowerInvariant() switch
    {
        "x64" or "amd64" => TargetArchitecture.X64,
        "arm64" or "aarch64" => TargetArchitecture.Arm64,
        _ => throw new BuildToolException($"Unsupported architecture '{value}'.")
    };

    private static BuildConfiguration ParseConfiguration(string value) => value.ToLowerInvariant() switch
    {
        "debug" => BuildConfiguration.Debug,
        "release" => BuildConfiguration.Release,
        _ => throw new BuildToolException($"Unsupported configuration '{value}'.")
    };
}
