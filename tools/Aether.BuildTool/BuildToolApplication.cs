namespace Aether.BuildTool;

internal static class BuildToolApplication
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            var arguments = CommandArguments.Parse(args);
            if (arguments.Command.Equals("help", StringComparison.OrdinalIgnoreCase) || arguments.Has("help"))
            {
                PrintHelp();
                return 0;
            }

            var root = RepositoryLocator.Resolve(arguments.Get("root"));
            var workspace = new WorkspaceScanner().Scan(root);
            switch (arguments.Command.ToLowerInvariant())
            {
                case "generate":
                    {
                        var solutionPath = await new VisualStudioGenerator().GenerateAsync(workspace, cancellationToken);
                        Console.WriteLine($"Generated Visual Studio solution: {solutionPath}");
                        return 0;
                    }
                case "build":
                    {
                        var target = BuildTarget.From(arguments);
                        await new NativeBuilder().BuildAsync(workspace, arguments.Get("target"), target, cancellationToken);
                        return 0;
                    }
                case "clean":
                    {
                        var target = BuildTarget.From(arguments);
                        new NativeBuilder().Clean(workspace, arguments.Get("target"), target);
                        return 0;
                    }
                case "doctor":
                    {
                        await RunDoctorAsync(workspace, BuildTarget.From(arguments), cancellationToken);
                        return 0;
                    }
                default:
                    throw new BuildToolException($"Unknown command '{arguments.Command}'.");
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Build cancelled.");
            return 2;
        }
        catch (BuildToolException exception)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static async Task RunDoctorAsync(
        Workspace workspace,
        BuildTarget target,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Repository: {workspace.RootDirectory}");
        Console.WriteLine($"Managed projects: {workspace.ManagedProjects.Count}");
        Console.WriteLine($"Native modules: {workspace.NativeModules.Count}");
        Console.WriteLine($"Requested target: {target.RuntimeIdentifier} {target.Configuration}");
        var toolchain = await NativeToolchainFactory.CreateAsync(target, cancellationToken);
        Console.WriteLine($"Toolchain: {toolchain.Name}");
        foreach (var module in workspace.NativeModules)
        {
            Console.WriteLine($"  {module.Name}: {module.Sources.Count} source(s), {module.Headers.Count} header(s)");
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Aether.BuildTool");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  generate  Generate the Visual Studio solution and NMake native projects.");
        Console.WriteLine("  build     Build native modules for the local host platform.");
        Console.WriteLine("  clean     Remove native outputs for a target.");
        Console.WriteLine("  doctor    Validate project discovery and the local C++ toolchain.");
        Console.WriteLine();
        Console.WriteLine("Common options:");
        Console.WriteLine("  --root <path>");
        Console.WriteLine("  --target <module>");
        Console.WriteLine("  --configuration <Debug|Release>");
        Console.WriteLine("  --platform <Windows|Linux|MacOS>");
        Console.WriteLine("  --architecture <x64|arm64>");
    }
}
