using System.Security;
using System.Text;

namespace Aether.BuildTool;

internal sealed class VisualStudioGenerator
{
    private const string SolutionFolderProjectType = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";
    private const string CSharpProjectType = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
    private const string CppProjectType = "{BC8A1FFA-BEE3-4634-8014-F334798102B3}";

    public async Task<string> GenerateAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        var outputDirectory = Path.Combine(workspace.RootDirectory, "Intermediate", "ProjectFiles");
        Directory.CreateDirectory(outputDirectory);

        foreach (var module in workspace.NativeModules)
        {
            await GenerateNativeProjectAsync(workspace, module, outputDirectory, cancellationToken);
        }

        var solutionPath = Path.Combine(outputDirectory, "Aether.sln");
        await FileWriter.WriteIfChangedAsync(
            solutionPath,
            GenerateSolution(workspace, outputDirectory),
            cancellationToken);
        return solutionPath;
    }

    private static string GenerateSolution(Workspace workspace, string outputDirectory)
    {
        var builder = new StringBuilder();
        var groups = workspace.ManagedProjects.Select(project => project.Group)
            .Concat(workspace.NativeModules.Count > 0 ? ["native"] : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        builder.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        builder.AppendLine("# Visual Studio Version 17");
        builder.AppendLine("VisualStudioVersion = 17.0.31903.59");
        builder.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");

        foreach (var group in groups)
        {
            builder.AppendLine($"Project(\"{SolutionFolderProjectType}\") = \"{group}\", \"{group}\", \"{FormatGuid(GroupGuid(group))}\"");
            builder.AppendLine("EndProject");
        }

        foreach (var project in workspace.ManagedProjects)
        {
            var relativePath = Path.GetRelativePath(outputDirectory, project.ProjectPath).Replace('/', '\\');
            builder.AppendLine($"Project(\"{CSharpProjectType}\") = \"{project.Name}\", \"{relativePath}\", \"{FormatGuid(ProjectGuid(project.ProjectPath, workspace.RootDirectory))}\"");
            builder.AppendLine("EndProject");
        }

        foreach (var module in workspace.NativeModules)
        {
            builder.AppendLine($"Project(\"{CppProjectType}\") = \"{module.Name}\", \"{module.Name}.vcxproj\", \"{FormatGuid(ProjectGuid(module.Manifest.ManifestPath, workspace.RootDirectory))}\"");
            builder.AppendLine("EndProject");
        }

        builder.AppendLine("Global");
        builder.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        builder.AppendLine("\t\tDebug|x64 = Debug|x64");
        builder.AppendLine("\t\tRelease|x64 = Release|x64");
        builder.AppendLine("\tEndGlobalSection");
        builder.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");

        foreach (var project in workspace.ManagedProjects)
        {
            var guid = FormatGuid(ProjectGuid(project.ProjectPath, workspace.RootDirectory));
            AppendManagedConfigurations(builder, guid);
        }

        foreach (var module in workspace.NativeModules)
        {
            var guid = FormatGuid(ProjectGuid(module.Manifest.ManifestPath, workspace.RootDirectory));
            AppendNativeConfigurations(builder, guid);
        }

        builder.AppendLine("\tEndGlobalSection");
        builder.AppendLine("\tGlobalSection(SolutionProperties) = preSolution");
        builder.AppendLine("\t\tHideSolutionNode = FALSE");
        builder.AppendLine("\tEndGlobalSection");
        builder.AppendLine("\tGlobalSection(NestedProjects) = preSolution");

        foreach (var project in workspace.ManagedProjects)
        {
            builder.AppendLine($"\t\t{FormatGuid(ProjectGuid(project.ProjectPath, workspace.RootDirectory))} = {FormatGuid(GroupGuid(project.Group))}");
        }

        foreach (var module in workspace.NativeModules)
        {
            builder.AppendLine($"\t\t{FormatGuid(ProjectGuid(module.Manifest.ManifestPath, workspace.RootDirectory))} = {FormatGuid(GroupGuid("native"))}");
        }

        builder.AppendLine("\tEndGlobalSection");
        builder.AppendLine("EndGlobal");
        return builder.ToString();
    }

    private static async Task GenerateNativeProjectAsync(
        Workspace workspace,
        NativeModule module,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var projectPath = Path.Combine(outputDirectory, module.Name + ".vcxproj");
        var filtersPath = projectPath + ".filters";
        var projectGuid = FormatGuid(ProjectGuid(module.Manifest.ManifestPath, workspace.RootDirectory));
        var includes = string.Join(';', module.IncludeDirectories.Select(path => ToProjectRelativePath(outputDirectory, path)));
        var definitions = string.Join(';', module.Manifest.Definitions.Concat(["AETHER_PLATFORM_WINDOWS=1", "%(NMakePreprocessorDefinitions)"]));
        var sourceItems = string.Join(Environment.NewLine, module.Sources.Select(path => $"    <ClCompile Include=\"{Xml(ToProjectRelativePath(outputDirectory, path))}\" />"));
        var headerItems = string.Join(Environment.NewLine, module.Headers.Select(path => $"    <ClInclude Include=\"{Xml(ToProjectRelativePath(outputDirectory, path))}\" />"));
        var rootArgument = "&quot;$(SolutionDir)..\\..&quot;";
        var buildTool = "&quot;$(SolutionDir)..\\BuildTool\\Aether.BuildTool.dll&quot;";
        var outputFile = Path.Combine(workspace.RootDirectory, "artifacts", "native", "win-x64", "$(Configuration)", module.Name + ".dll");
        var buildCommand = $"dotnet {buildTool} build --root {rootArgument} --target &quot;{Xml(module.Name)}&quot; --configuration &quot;$(Configuration)&quot; --platform Windows --architecture x64";
        var cleanCommand = $"dotnet {buildTool} clean --root {rootArgument} --target &quot;{Xml(module.Name)}&quot; --configuration &quot;$(Configuration)&quot; --platform Windows --architecture x64";

        var project = $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup Label="ProjectConfigurations">
                <ProjectConfiguration Include="Debug|x64">
                  <Configuration>Debug</Configuration>
                  <Platform>x64</Platform>
                </ProjectConfiguration>
                <ProjectConfiguration Include="Release|x64">
                  <Configuration>Release</Configuration>
                  <Platform>x64</Platform>
                </ProjectConfiguration>
              </ItemGroup>
              <PropertyGroup Label="Globals">
                <VCProjectVersion>17.0</VCProjectVersion>
                <ProjectGuid>{{projectGuid}}</ProjectGuid>
                <Keyword>MakeFileProj</Keyword>
                <RootNamespace>{{Xml(module.Name)}}</RootNamespace>
              </PropertyGroup>
              <Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
              <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'" Label="Configuration">
                <ConfigurationType>Makefile</ConfigurationType>
                <PlatformToolset>v143</PlatformToolset>
                <UseDebugLibraries>true</UseDebugLibraries>
              </PropertyGroup>
              <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|x64'" Label="Configuration">
                <ConfigurationType>Makefile</ConfigurationType>
                <PlatformToolset>v143</PlatformToolset>
                <UseDebugLibraries>false</UseDebugLibraries>
              </PropertyGroup>
              <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
              <PropertyGroup>
                <NMakeBuildCommandLine>{{buildCommand}}</NMakeBuildCommandLine>
                <NMakeReBuildCommandLine>{{cleanCommand}} &amp;&amp; {{buildCommand}}</NMakeReBuildCommandLine>
                <NMakeCleanCommandLine>{{cleanCommand}}</NMakeCleanCommandLine>
                <NMakeOutput>{{Xml(outputFile)}}</NMakeOutput>
                <NMakeIncludeSearchPath>{{Xml(includes)}};$(NMakeIncludeSearchPath)</NMakeIncludeSearchPath>
                <NMakePreprocessorDefinitions>{{Xml(definitions)}}</NMakePreprocessorDefinitions>
                <AdditionalOptions>/std:c++20 /permissive- /Zc:__cplusplus %(AdditionalOptions)</AdditionalOptions>
              </PropertyGroup>
              <ItemGroup>
            {{sourceItems}}
            {{headerItems}}
                <None Include="{{Xml(ToProjectRelativePath(outputDirectory, module.Manifest.ManifestPath))}}" />
              </ItemGroup>
              <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
            </Project>
            """;

        await FileWriter.WriteIfChangedAsync(projectPath, project, cancellationToken);
        await FileWriter.WriteIfChangedAsync(filtersPath, GenerateFilters(module, outputDirectory), cancellationToken);
    }

    private static string GenerateFilters(NativeModule module, string outputDirectory)
    {
        var sourceItems = string.Join(Environment.NewLine, module.Sources.Select(path =>
            $"    <ClCompile Include=\"{Xml(ToProjectRelativePath(outputDirectory, path))}\"><Filter>{Xml(GetFilter(module, path))}</Filter></ClCompile>"));
        var headerItems = string.Join(Environment.NewLine, module.Headers.Select(path =>
            $"    <ClInclude Include=\"{Xml(ToProjectRelativePath(outputDirectory, path))}\"><Filter>{Xml(GetFilter(module, path))}</Filter></ClInclude>"));

        return $$"""
            <?xml version="1.0" encoding="utf-8"?>
            <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <ItemGroup>
                <Filter Include="Public"><UniqueIdentifier>{{FormatGuid(DeterministicGuid.Create(module.Name + ":Public"))}}</UniqueIdentifier></Filter>
                <Filter Include="Private"><UniqueIdentifier>{{FormatGuid(DeterministicGuid.Create(module.Name + ":Private"))}}</UniqueIdentifier></Filter>
              </ItemGroup>
              <ItemGroup>
            {{sourceItems}}
            {{headerItems}}
              </ItemGroup>
            </Project>
            """;
    }

    private static string GetFilter(NativeModule module, string path)
    {
        var relative = Path.GetRelativePath(module.ModuleDirectory, path);
        var first = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return first.Equals("Public", StringComparison.OrdinalIgnoreCase) ? "Public" : "Private";
    }

    private static void AppendManagedConfigurations(StringBuilder builder, string guid)
    {
        builder.AppendLine($"\t\t{guid}.Debug|x64.ActiveCfg = Debug|Any CPU");
        builder.AppendLine($"\t\t{guid}.Debug|x64.Build.0 = Debug|Any CPU");
        builder.AppendLine($"\t\t{guid}.Release|x64.ActiveCfg = Release|Any CPU");
        builder.AppendLine($"\t\t{guid}.Release|x64.Build.0 = Release|Any CPU");
    }

    private static void AppendNativeConfigurations(StringBuilder builder, string guid)
    {
        builder.AppendLine($"\t\t{guid}.Debug|x64.ActiveCfg = Debug|x64");
        builder.AppendLine($"\t\t{guid}.Debug|x64.Build.0 = Debug|x64");
        builder.AppendLine($"\t\t{guid}.Release|x64.ActiveCfg = Release|x64");
        builder.AppendLine($"\t\t{guid}.Release|x64.Build.0 = Release|x64");
    }

    private static Guid ProjectGuid(string projectPath, string rootDirectory) =>
        DeterministicGuid.Create("project:" + Path.GetRelativePath(rootDirectory, projectPath));

    private static Guid GroupGuid(string group) => DeterministicGuid.Create("group:" + group);

    private static string FormatGuid(Guid guid) => guid.ToString("B").ToUpperInvariant();

    private static string ToProjectRelativePath(string outputDirectory, string path) =>
        Path.GetRelativePath(outputDirectory, path).Replace('/', '\\');

    private static string Xml(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
