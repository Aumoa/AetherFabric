namespace Aether.BuildTool;

internal sealed class NativeBuilder
{
    public async Task BuildAsync(
        Workspace workspace,
        string? targetName,
        BuildTarget target,
        CancellationToken cancellationToken)
    {
        var modules = ResolveBuildOrder(workspace.NativeModules, targetName);
        var toolchain = await NativeToolchainFactory.CreateAsync(target, cancellationToken);
        Console.WriteLine($"Toolchain: {toolchain.Name}");
        Console.WriteLine($"Target: {target.RuntimeIdentifier} {target.Configuration}");

        foreach (var module in modules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await toolchain.BuildAsync(module, workspace.RootDirectory, target, cancellationToken);
        }
    }

    public void Clean(Workspace workspace, string? targetName, BuildTarget target)
    {
        var modules = ResolveBuildOrder(workspace.NativeModules, targetName);
        foreach (var module in modules)
        {
            var paths = new NativeBuildPaths(workspace.RootDirectory, module, target);
            DeleteDirectoryIfContained(workspace.RootDirectory, paths.IntermediateDirectory);

            foreach (var output in OutputFiles(module, paths, target))
            {
                DeleteFileIfContained(workspace.RootDirectory, output);
            }
        }
    }

    internal static IReadOnlyList<NativeModule> ResolveBuildOrder(
        IReadOnlyList<NativeModule> modules,
        string? targetName)
    {
        var byName = modules.ToDictionary(module => module.Name, StringComparer.OrdinalIgnoreCase);
        IEnumerable<NativeModule> roots;
        if (string.IsNullOrWhiteSpace(targetName))
        {
            roots = modules;
        }
        else if (byName.TryGetValue(targetName, out var target))
        {
            roots = [target];
        }
        else
        {
            throw new BuildToolException($"Native target '{targetName}' was not found.");
        }

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<NativeModule>();
        foreach (var root in roots)
        {
            Visit(root);
        }

        return result;

        void Visit(NativeModule module)
        {
            if (visited.Contains(module.Name))
            {
                return;
            }

            if (!visiting.Add(module.Name))
            {
                throw new BuildToolException($"Cyclic native module dependency detected at '{module.Name}'.");
            }

            foreach (var dependencyName in module.Manifest.Dependencies)
            {
                if (!byName.TryGetValue(dependencyName, out var dependency))
                {
                    throw new BuildToolException($"Native module '{module.Name}' depends on missing module '{dependencyName}'.");
                }

                Visit(dependency);
            }

            visiting.Remove(module.Name);
            visited.Add(module.Name);
            result.Add(module);
        }
    }

    private static IEnumerable<string> OutputFiles(NativeModule module, NativeBuildPaths paths, BuildTarget target)
    {
        yield return paths.OutputFile;
        if (target.Platform == TargetPlatform.Windows)
        {
            yield return Path.Combine(paths.OutputDirectory, module.Name + ".lib");
            yield return Path.Combine(paths.OutputDirectory, module.Name + ".exp");
            yield return Path.Combine(paths.OutputDirectory, module.Name + ".pdb");
        }
    }

    private static void DeleteDirectoryIfContained(string root, string path)
    {
        var resolvedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolvedPath = Path.GetFullPath(path);
        if (!resolvedPath.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new BuildToolException($"Refusing to delete directory outside the repository: '{resolvedPath}'.");
        }

        if (Directory.Exists(resolvedPath))
        {
            Directory.Delete(resolvedPath, recursive: true);
            Console.WriteLine($"Removed {Path.GetRelativePath(root, resolvedPath)}");
        }
    }

    private static void DeleteFileIfContained(string root, string path)
    {
        var resolvedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolvedPath = Path.GetFullPath(path);
        if (!resolvedPath.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new BuildToolException($"Refusing to delete file outside the repository: '{resolvedPath}'.");
        }

        if (File.Exists(resolvedPath))
        {
            File.Delete(resolvedPath);
            Console.WriteLine($"Removed {Path.GetRelativePath(root, resolvedPath)}");
        }
    }
}
