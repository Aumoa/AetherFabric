namespace Aether.BuildTool;

internal sealed record ManagedProject(string Name, string ProjectPath, string Group);

internal sealed record NativeModule(
    NativeModuleManifest Manifest,
    string ModuleDirectory,
    IReadOnlyList<string> Sources,
    IReadOnlyList<string> Headers)
{
    public string Name => Manifest.Name;

    public IReadOnlyList<string> IncludeDirectories => Manifest.PublicIncludeDirectories
        .Concat(Manifest.PrivateIncludeDirectories)
        .Select(path => Path.GetFullPath(Path.Combine(ModuleDirectory, path)))
        .ToArray();
}

internal sealed record Workspace(
    string RootDirectory,
    IReadOnlyList<ManagedProject> ManagedProjects,
    IReadOnlyList<NativeModule> NativeModules);
