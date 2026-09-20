namespace Aether.BuildTool;

internal sealed class MsvcToolchain : INativeToolchain
{
    private readonly string _developerCommandFile;

    private MsvcToolchain(string developerCommandFile)
    {
        _developerCommandFile = developerCommandFile;
    }

    public string Name => "MSVC";

    public static async Task<MsvcToolchain> CreateAsync(CancellationToken cancellationToken)
    {
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var vswhere = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        if (!File.Exists(vswhere))
        {
            throw new BuildToolException("Visual Studio Installer's vswhere.exe was not found. Install the Desktop development with C++ workload.");
        }

        var result = await ProcessRunner.RunAsync(
            vswhere,
            ["-latest", "-products", "*", "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-property", "installationPath"],
            Environment.CurrentDirectory,
            cancellationToken,
            echoOutput: false);
        var installationPath = result.StandardOutput.Trim();
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(installationPath))
        {
            throw new BuildToolException("A Visual Studio installation with the C++ toolchain was not found.");
        }

        var developerCommandFile = Path.Combine(installationPath, "Common7", "Tools", "VsDevCmd.bat");
        if (!File.Exists(developerCommandFile))
        {
            throw new BuildToolException($"Visual Studio developer command file was not found at '{developerCommandFile}'.");
        }

        return new MsvcToolchain(developerCommandFile);
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
            var objectFile = paths.ObjectFile(source, ".obj");
            objectFiles.Add(objectFile);
            if (!NeedsCompile(module, source, objectFile))
            {
                continue;
            }

            Console.WriteLine($"[CXX] {Path.GetRelativePath(repositoryRoot, source)}");
            var responseFile = objectFile + ".rsp";
            await File.WriteAllLinesAsync(responseFile, CompileArguments(module, source, objectFile, target), cancellationToken);
            await RunDeveloperCommandAsync(
                $"cl.exe @{Quote(responseFile)}",
                repositoryRoot,
                target.Architecture,
                cancellationToken);
        }

        if (!NeedsLink(paths.OutputFile, objectFiles))
        {
            Console.WriteLine($"[UP-TO-DATE] {module.Name}");
            return;
        }

        Console.WriteLine($"[LINK] {Path.GetRelativePath(repositoryRoot, paths.OutputFile)}");
        var linkResponseFile = Path.Combine(paths.IntermediateDirectory, module.Name + ".link.rsp");
        await File.WriteAllLinesAsync(linkResponseFile, LinkArguments(module, paths, objectFiles, target), cancellationToken);
        await RunDeveloperCommandAsync(
            $"link.exe @{Quote(linkResponseFile)}",
            repositoryRoot,
            target.Architecture,
            cancellationToken);
    }

    private async Task RunDeveloperCommandAsync(
        string command,
        string workingDirectory,
        TargetArchitecture architecture,
        CancellationToken cancellationToken)
    {
        var architectureName = architecture == TargetArchitecture.X64 ? "x64" : "arm64";
        var scriptDirectory = Path.Combine(workingDirectory, "artifacts", "obj", "native");
        Directory.CreateDirectory(scriptDirectory);
        var scriptPath = Path.Combine(scriptDirectory, $"msvc-invoke-{Guid.NewGuid():N}.cmd");
        var script = $"""
            @ECHO OFF
            CALL "{_developerCommandFile}" -no_logo -arch={architectureName} -host_arch=x64 >NUL
            IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%
            {command}
            EXIT /B %ERRORLEVEL%
            """;

        await File.WriteAllTextAsync(scriptPath, script.ReplaceLineEndings("\r\n"), cancellationToken);
        try
        {
            await ProcessRunner.RunCheckedAsync("cmd.exe", ["/d", "/c", scriptPath], workingDirectory, cancellationToken);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    private static IEnumerable<string> CompileArguments(NativeModule module, string source, string objectFile, BuildTarget target)
    {
        yield return "/nologo";
        yield return "/c";
        yield return "/std:c++20";
        yield return "/EHsc";
        yield return "/utf-8";
        yield return "/permissive-";
        yield return "/Zc:__cplusplus";
        yield return target.Configuration == BuildConfiguration.Debug ? "/MDd" : "/MD";
        yield return target.Configuration == BuildConfiguration.Debug ? "/Od" : "/O2";
        if (target.Configuration == BuildConfiguration.Debug)
        {
            yield return "/Zi";
            yield return "/Fd" + Quote(Path.Combine(Path.GetDirectoryName(objectFile)!, module.Name + ".compile.pdb"));
        }
        else
        {
            yield return "/DNDEBUG";
        }

        yield return "/DAETHER_PLATFORM_WINDOWS=1";
        foreach (var definition in module.Manifest.Definitions)
        {
            yield return "/D" + definition;
        }

        foreach (var includeDirectory in module.IncludeDirectories)
        {
            yield return "/I" + Quote(includeDirectory);
        }

        yield return "/Fo" + Quote(objectFile);
        yield return Quote(source);
    }

    private static IEnumerable<string> LinkArguments(
        NativeModule module,
        NativeBuildPaths paths,
        IEnumerable<string> objectFiles,
        BuildTarget target)
    {
        yield return "/nologo";
        yield return "/DLL";
        yield return "/OUT:" + Quote(paths.OutputFile);
        yield return "/IMPLIB:" + Quote(Path.Combine(paths.OutputDirectory, module.Name + ".lib"));
        if (target.Configuration == BuildConfiguration.Debug)
        {
            yield return "/DEBUG";
            yield return "/PDB:" + Quote(Path.Combine(paths.OutputDirectory, module.Name + ".pdb"));
        }

        foreach (var objectFile in objectFiles)
        {
            yield return Quote(objectFile);
        }
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

    private static string Quote(string value) => '"' + value.Replace("\"", "\\\"") + '"';
}
