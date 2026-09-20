namespace Aether.BuildTool;

internal sealed class WorkspaceScanner
{
    private static readonly string[] ManagedRoots = ["src", "tests", "tools"];

    public Workspace Scan(string rootDirectory)
    {
        var root = Path.GetFullPath(rootDirectory);
        var managedProjects = ManagedRoots
            .SelectMany(group => ScanManagedProjects(root, group))
            .OrderBy(project => project.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var nativeModules = ScanNativeModules(root)
            .OrderBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        EnsureUniqueNames(managedProjects, nativeModules);
        return new Workspace(root, managedProjects, nativeModules);
    }

    private static IEnumerable<ManagedProject> ScanManagedProjects(string root, string group)
    {
        var directory = Path.Combine(root, group);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(Path.GetRelativePath(directory, path)))
            .Select(path => new ManagedProject(Path.GetFileNameWithoutExtension(path), Path.GetFullPath(path), group));
    }

    private static IEnumerable<NativeModule> ScanNativeModules(string root)
    {
        var nativeRoot = Path.Combine(root, "native");
        if (!Directory.Exists(nativeRoot))
        {
            return [];
        }

        return Directory.EnumerateFiles(nativeRoot, "*.Module.json", SearchOption.AllDirectories)
            .Select(NativeModuleManifest.Load)
            .Select(CreateNativeModule);
    }

    private static NativeModule CreateNativeModule(NativeModuleManifest manifest)
    {
        var moduleDirectory = Path.GetDirectoryName(manifest.ManifestPath)!;
        var sources = manifest.SourceDirectories
            .Select(path => Path.Combine(moduleDirectory, path))
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".cc", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".cxx", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var headers = manifest.PublicIncludeDirectories
            .Concat(manifest.PrivateIncludeDirectories)
            .Select(path => Path.Combine(moduleDirectory, path))
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".h", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".hpp", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sources.Length == 0)
        {
            throw new BuildToolException($"Native module '{manifest.Name}' does not contain any C++ source files.");
        }

        return new NativeModule(manifest, moduleDirectory, sources, headers);
    }

    private static bool IsGeneratedPath(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                                       segment.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                                       segment.Equals("Intermediate", StringComparison.OrdinalIgnoreCase) ||
                                       segment.Equals("artifacts", StringComparison.OrdinalIgnoreCase));
    }

    private static void EnsureUniqueNames(IEnumerable<ManagedProject> managedProjects, IEnumerable<NativeModule> nativeModules)
    {
        var duplicate = managedProjects.Select(project => project.Name)
            .Concat(nativeModules.Select(module => module.Name))
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new BuildToolException($"Project name '{duplicate.Key}' is used more than once.");
        }
    }
}
