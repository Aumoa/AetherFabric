namespace Aether.BuildTool;

internal static class PlatformDefinitions
{
    private static readonly HashSet<string> ReservedNames =
    [
        "PLATFORM_WINDOWS",
        "PLATFORM_LINUX",
        "PLATFORM_MACOS"
    ];

    public static IReadOnlyList<string> For(TargetPlatform platform) => platform switch
    {
        TargetPlatform.Windows =>
        [
            "PLATFORM_WINDOWS=1",
            "PLATFORM_LINUX=0",
            "PLATFORM_MACOS=0"
        ],
        TargetPlatform.Linux =>
        [
            "PLATFORM_WINDOWS=0",
            "PLATFORM_LINUX=1",
            "PLATFORM_MACOS=0"
        ],
        TargetPlatform.MacOS =>
        [
            "PLATFORM_WINDOWS=0",
            "PLATFORM_LINUX=0",
            "PLATFORM_MACOS=1"
        ],
        _ => throw new BuildToolException($"Unsupported target platform '{platform}'.")
    };

    public static bool IsReserved(string definition)
    {
        var separator = definition.IndexOf('=');
        var name = (separator < 0 ? definition : definition[..separator]).Trim();
        return ReservedNames.Contains(name);
    }
}
