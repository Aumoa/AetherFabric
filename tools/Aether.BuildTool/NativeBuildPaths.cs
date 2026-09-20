namespace Aether.BuildTool;

internal sealed class NativeBuildPaths
{
    public NativeBuildPaths(string repositoryRoot, NativeModule module, BuildTarget target)
    {
        OutputDirectory = Path.Combine(
            repositoryRoot,
            "artifacts",
            "native",
            target.RuntimeIdentifier,
            target.Configuration.ToString());
        IntermediateDirectory = Path.Combine(
            repositoryRoot,
            "artifacts",
            "obj",
            "native",
            target.RuntimeIdentifier,
            target.Configuration.ToString(),
            module.Name);
        OutputFile = Path.Combine(OutputDirectory, GetOutputFileName(module.Name, target.Platform));
    }

    public string OutputDirectory { get; }

    public string IntermediateDirectory { get; }

    public string OutputFile { get; }

    public string ObjectFile(string sourcePath, string extension)
    {
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var identity = DeterministicGuid.Create(sourcePath).ToString("N")[..8];
        return Path.Combine(IntermediateDirectory, $"{name}.{identity}{extension}");
    }

    public static string GetOutputFileName(string moduleName, TargetPlatform platform) => platform switch
    {
        TargetPlatform.Windows => moduleName + ".dll",
        TargetPlatform.Linux => "lib" + moduleName + ".so",
        TargetPlatform.MacOS => "lib" + moduleName + ".dylib",
        _ => throw new BuildToolException($"Unsupported platform '{platform}'.")
    };
}
