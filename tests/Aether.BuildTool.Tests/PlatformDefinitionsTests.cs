using Aether.BuildTool;

namespace Aether.BuildTool.Tests;

public sealed class PlatformDefinitionsTests
{
    [Theory]
    [InlineData("Windows", "PLATFORM_WINDOWS=1")]
    [InlineData("Linux", "PLATFORM_LINUX=1")]
    [InlineData("MacOS", "PLATFORM_MACOS=1")]
    public void For_DefinesExactlyOneActivePlatform(string platformName, string activeDefinition)
    {
        var platform = Enum.Parse<TargetPlatform>(platformName);
        var definitions = PlatformDefinitions.For(platform);

        Assert.Equal(3, definitions.Count);
        Assert.Contains(activeDefinition, definitions);
        Assert.Single(definitions, definition => definition.EndsWith("=1", StringComparison.Ordinal));
        Assert.Equal(2, definitions.Count(definition => definition.EndsWith("=0", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("PLATFORM_WINDOWS=0")]
    [InlineData("PLATFORM_LINUX")]
    [InlineData(" PLATFORM_MACOS = 1")]
    public void IsReserved_RecognizesBuiltInPlatformDefinitions(string definition)
    {
        Assert.True(PlatformDefinitions.IsReserved(definition));
    }

    [Fact]
    public void ManifestLoad_RejectsPlatformDefinitionOverride()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "TestArtifacts", Guid.NewGuid().ToString("N"));
        var manifestPath = Path.Combine(root, "Aether.Native.Module.json");
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(
                manifestPath,
                """
                {
                  "name": "Aether.Native",
                  "definitions": ["PLATFORM_WINDOWS=0"]
                }
                """);

            var exception = Assert.Throws<BuildToolException>(() => NativeModuleManifest.Load(manifestPath));

            Assert.Contains("cannot override built-in platform definition", exception.Message);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
