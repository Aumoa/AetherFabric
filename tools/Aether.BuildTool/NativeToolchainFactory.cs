using System.Runtime.InteropServices;

namespace Aether.BuildTool;

internal static class NativeToolchainFactory
{
    public static async Task<INativeToolchain> CreateAsync(BuildTarget target, CancellationToken cancellationToken)
    {
        var hostPlatform = BuildTarget.GetHostPlatform();
        var hostArchitecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => TargetArchitecture.X64,
            Architecture.Arm64 => TargetArchitecture.Arm64,
            _ => throw new BuildToolException($"Unsupported host architecture '{RuntimeInformation.ProcessArchitecture}'.")
        };

        if (target.Platform != hostPlatform)
        {
            throw new BuildToolException(
                $"Cross-host native builds are not supported. Build '{target.Platform}' targets on a {target.Platform} host or CI runner.");
        }

        if (target.Architecture != hostArchitecture)
        {
            throw new BuildToolException(
                $"Cross-architecture native builds are not configured. Current host is '{hostArchitecture}', requested '{target.Architecture}'.");
        }

        return target.Platform switch
        {
            TargetPlatform.Windows => await MsvcToolchain.CreateAsync(cancellationToken),
            TargetPlatform.Linux => await UnixToolchain.CreateAsync(isMacOS: false, cancellationToken),
            TargetPlatform.MacOS => await UnixToolchain.CreateAsync(isMacOS: true, cancellationToken),
            _ => throw new BuildToolException($"Unsupported platform '{target.Platform}'.")
        };
    }
}
