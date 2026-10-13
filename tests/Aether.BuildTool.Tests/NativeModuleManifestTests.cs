using Aether.BuildTool;

namespace Aether.BuildTool.Tests;

public sealed class NativeModuleManifestTests
{
    [Theory]
    [InlineData("c++17")]
    [InlineData("c++20")]
    [InlineData("c++26")]
    public void Load_RejectsStandardsOutsideSelectedBaseline(string standard)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "TestArtifacts", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "Native.Module.json");
            File.WriteAllText(path, $$"""
                { "name": "Aether.Native", "languageStandard": "{{standard}}" }
                """);

            var exception = Assert.Throws<BuildToolException>(() => NativeModuleManifest.Load(path));
            Assert.Contains("unsupported language standard", exception.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
