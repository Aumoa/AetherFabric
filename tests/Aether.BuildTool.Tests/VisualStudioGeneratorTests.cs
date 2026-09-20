using Aether.BuildTool;

namespace Aether.BuildTool.Tests;

public sealed class VisualStudioGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_IncludesManagedAndNativeProjects()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "TestArtifacts", Guid.NewGuid().ToString("N"));
        try
        {
            CreateWorkspace(root);
            var workspace = new WorkspaceScanner().Scan(root);

            var solutionPath = await new VisualStudioGenerator().GenerateAsync(workspace, CancellationToken.None);

            var solution = await File.ReadAllTextAsync(solutionPath);
            var nativeProject = await File.ReadAllTextAsync(
                Path.Combine(root, "Intermediate", "ProjectFiles", "Aether.Native.vcxproj"));
            Assert.Contains("Aether.Sample.csproj", solution);
            Assert.Contains("Aether.Native.vcxproj", solution);
            Assert.Contains("<ConfigurationType>Makefile</ConfigurationType>", nativeProject);
            Assert.Contains("Aether.BuildTool.dll", nativeProject);
            Assert.Contains("NMakeBuildCommandLine", nativeProject);
            Assert.Contains("PLATFORM_WINDOWS=1;PLATFORM_LINUX=0;PLATFORM_MACOS=0", nativeProject);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static void CreateWorkspace(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "src", "Aether.Sample"));
        Directory.CreateDirectory(Path.Combine(root, "native", "Aether.Native", "Public"));
        Directory.CreateDirectory(Path.Combine(root, "native", "Aether.Native", "Private"));
        File.WriteAllText(Path.Combine(root, "global.json"), "{}");
        File.WriteAllText(
            Path.Combine(root, "src", "Aether.Sample", "Aether.Sample.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(
            Path.Combine(root, "native", "Aether.Native", "Aether.Native.Module.json"),
            """
            {
              "name": "Aether.Native",
              "publicIncludeDirectories": ["Public"],
              "privateIncludeDirectories": ["Private"],
              "sourceDirectories": ["Private"]
            }
            """);
        File.WriteAllText(
            Path.Combine(root, "native", "Aether.Native", "Public", "Aether.Native.h"),
            "#pragma once");
        File.WriteAllText(
            Path.Combine(root, "native", "Aether.Native", "Private", "Aether.Native.cpp"),
            "int value = 0;");
    }
}
