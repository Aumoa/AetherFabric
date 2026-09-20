namespace Aether.BuildTool;

internal static class RepositoryLocator
{
    public static string Resolve(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            var root = Path.GetFullPath(explicitRoot);
            Validate(root);
            return root;
        }

        var current = new DirectoryInfo(Environment.CurrentDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(current.FullName, "src")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new BuildToolException("Could not locate the Aether repository root. Pass --root explicitly.");
    }

    private static void Validate(string root)
    {
        if (!Directory.Exists(root) || !File.Exists(Path.Combine(root, "global.json")))
        {
            throw new BuildToolException($"'{root}' is not an Aether repository root.");
        }
    }
}
