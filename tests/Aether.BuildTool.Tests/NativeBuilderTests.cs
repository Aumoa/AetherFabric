using Aether.BuildTool;

namespace Aether.BuildTool.Tests;

public sealed class NativeBuilderTests
{
    [Fact]
    public void ResolveBuildOrder_PlacesDependenciesBeforeTarget()
    {
        var core = CreateModule("Aether.Native.Core", []);
        var transport = CreateModule("Aether.Native.Transport", [core.Name]);

        var result = NativeBuilder.ResolveBuildOrder([transport, core], transport.Name);

        Assert.Equal([core.Name, transport.Name], result.Select(module => module.Name));
    }

    private static NativeModule CreateModule(string name, string[] dependencies)
    {
        var manifest = new NativeModuleManifest
        {
            Name = name,
            ManifestPath = Path.Combine("native", name, name + ".Module.json"),
            Dependencies = dependencies
        };
        return new NativeModule(manifest, Path.Combine("native", name), [name + ".cpp"], []);
    }
}
