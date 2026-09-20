using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aether.BuildTool;

internal sealed class NativeModuleManifest
{
    public required string Name { get; init; }

    public string Kind { get; init; } = "sharedLibrary";

    public string LanguageStandard { get; init; } = "c++20";

    public string[] PublicIncludeDirectories { get; init; } = [];

    public string[] PrivateIncludeDirectories { get; init; } = [];

    public string[] SourceDirectories { get; init; } = [];

    public string[] Definitions { get; init; } = [];

    public string[] Dependencies { get; init; } = [];

    [JsonIgnore]
    public string ManifestPath { get; init; } = string.Empty;

    public static NativeModuleManifest Load(string manifestPath)
    {
        try
        {
            var json = File.ReadAllText(manifestPath);
            var manifest = JsonSerializer.Deserialize<NativeModuleManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (manifest is null)
            {
                throw new BuildToolException($"Native module manifest '{manifestPath}' is empty.");
            }

            manifest = new NativeModuleManifest
            {
                Name = manifest.Name,
                Kind = manifest.Kind,
                LanguageStandard = manifest.LanguageStandard,
                PublicIncludeDirectories = manifest.PublicIncludeDirectories,
                PrivateIncludeDirectories = manifest.PrivateIncludeDirectories,
                SourceDirectories = manifest.SourceDirectories,
                Definitions = manifest.Definitions,
                Dependencies = manifest.Dependencies,
                ManifestPath = Path.GetFullPath(manifestPath)
            };
            manifest.Validate();
            return manifest;
        }
        catch (JsonException exception)
        {
            throw new BuildToolException($"Failed to parse native module manifest '{manifestPath}'.", exception);
        }
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new BuildToolException($"Native module manifest '{ManifestPath}' must define a name.");
        }

        if (!string.Equals(Kind, "sharedLibrary", StringComparison.OrdinalIgnoreCase))
        {
            throw new BuildToolException($"Native module '{Name}' uses unsupported kind '{Kind}'.");
        }

        if (!string.Equals(LanguageStandard, "c++20", StringComparison.OrdinalIgnoreCase))
        {
            throw new BuildToolException($"Native module '{Name}' uses unsupported language standard '{LanguageStandard}'.");
        }

        var reservedDefinition = Definitions.FirstOrDefault(PlatformDefinitions.IsReserved);
        if (reservedDefinition is not null)
        {
            throw new BuildToolException(
                $"Native module '{Name}' cannot override built-in platform definition '{reservedDefinition}'.");
        }

        foreach (var relativePath in PublicIncludeDirectories.Concat(PrivateIncludeDirectories).Concat(SourceDirectories))
        {
            var segments = relativePath.Replace('\\', '/').Split('/');
            if (Path.IsPathRooted(relativePath) || segments.Contains(".."))
            {
                throw new BuildToolException($"Native module '{Name}' contains unsafe path '{relativePath}'.");
            }
        }
    }
}
