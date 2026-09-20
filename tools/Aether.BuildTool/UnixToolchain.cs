namespace Aether.BuildTool;

internal sealed class UnixToolchain : INativeToolchain
{
    private readonly string _compiler;
    private readonly bool _isMacOS;

    private UnixToolchain(string compiler, bool isMacOS)
    {
        _compiler = compiler;
        _isMacOS = isMacOS;
    }

    public string Name => _isMacOS ? "Apple Clang" : _compiler;

    public static async Task<UnixToolchain> CreateAsync(bool isMacOS, CancellationToken cancellationToken)
    {
        var candidates = isMacOS ? new[] { "clang++" } : new[] { "clang++", "g++" };
        foreach (var candidate in candidates)
        {
            try
            {
                var result = await ProcessRunner.RunAsync(
                    candidate,
                    ["--version"],
                    Environment.CurrentDirectory,
                    cancellationToken,
                    echoOutput: false);
                if (result.ExitCode == 0)
                {
                    return new UnixToolchain(candidate, isMacOS);
                }
            }
            catch (BuildToolException)
            {
                // Try the next supported compiler.
            }
        }

        throw new BuildToolException(isMacOS
            ? "Apple Clang was not found. Install Xcode command line tools."
            : "Neither clang++ nor g++ was found. Install a C++20 compiler.");
    }

    public async Task BuildAsync(
        NativeModule module,
        string repositoryRoot,
        BuildTarget target,
        CancellationToken cancellationToken)
    {
        var paths = new NativeBuildPaths(repositoryRoot, module, target);
        Directory.CreateDirectory(paths.OutputDirectory);
        Directory.CreateDirectory(paths.IntermediateDirectory);

        var objectFiles = new List<string>();
        foreach (var source in module.Sources)
        {
            var objectFile = paths.ObjectFile(source, ".o");
            objectFiles.Add(objectFile);
            if (!NeedsCompile(module, source, objectFile))
            {
                continue;
            }

            Console.WriteLine($"[CXX] {Path.GetRelativePath(repositoryRoot, source)}");
            var arguments = new List<string>
            {
                "-std=c++20",
                "-fPIC",
                "-fvisibility=hidden",
                "-c",
                source,
                "-o",
                objectFile,
                target.Configuration == BuildConfiguration.Debug ? "-O0" : "-O3"
            };
            if (target.Configuration == BuildConfiguration.Debug)
            {
                arguments.Add("-g");
            }
            else
            {
                arguments.Add("-DNDEBUG");
            }

            arguments.AddRange(PlatformDefinitions.For(target.Platform).Select(definition => "-D" + definition));
            arguments.AddRange(module.Manifest.Definitions.Select(definition => "-D" + definition));
            foreach (var includeDirectory in module.IncludeDirectories)
            {
                arguments.Add("-I" + includeDirectory);
            }

            await ProcessRunner.RunCheckedAsync(_compiler, arguments, repositoryRoot, cancellationToken);
        }

        if (!NeedsLink(paths.OutputFile, objectFiles))
        {
            Console.WriteLine($"[UP-TO-DATE] {module.Name}");
            return;
        }

        Console.WriteLine($"[LINK] {Path.GetRelativePath(repositoryRoot, paths.OutputFile)}");
        var linkArguments = new List<string> { _isMacOS ? "-dynamiclib" : "-shared", "-o", paths.OutputFile };
        linkArguments.AddRange(objectFiles);
        await ProcessRunner.RunCheckedAsync(_compiler, linkArguments, repositoryRoot, cancellationToken);
    }

    private static bool NeedsCompile(NativeModule module, string source, string objectFile)
    {
        if (!File.Exists(objectFile))
        {
            return true;
        }

        var objectTime = File.GetLastWriteTimeUtc(objectFile);
        return File.GetLastWriteTimeUtc(source) > objectTime ||
               File.GetLastWriteTimeUtc(module.Manifest.ManifestPath) > objectTime ||
               module.Headers.Any(header => File.GetLastWriteTimeUtc(header) > objectTime);
    }

    private static bool NeedsLink(string outputFile, IEnumerable<string> objectFiles)
    {
        if (!File.Exists(outputFile))
        {
            return true;
        }

        var outputTime = File.GetLastWriteTimeUtc(outputFile);
        return objectFiles.Any(objectFile => File.GetLastWriteTimeUtc(objectFile) > outputTime);
    }
}
