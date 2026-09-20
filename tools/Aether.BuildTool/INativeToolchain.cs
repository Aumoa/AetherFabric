namespace Aether.BuildTool;

internal interface INativeToolchain
{
    string Name { get; }

    Task BuildAsync(
        NativeModule module,
        string repositoryRoot,
        BuildTarget target,
        CancellationToken cancellationToken);
}
