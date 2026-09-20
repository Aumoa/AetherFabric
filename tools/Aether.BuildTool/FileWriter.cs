namespace Aether.BuildTool;

internal static class FileWriter
{
    public static async Task WriteIfChangedAsync(string path, string content, CancellationToken cancellationToken)
    {
        content = content.ReplaceLineEndings(Environment.NewLine);
        if (File.Exists(path))
        {
            var existing = await File.ReadAllTextAsync(path, cancellationToken);
            if (string.Equals(existing, content, StringComparison.Ordinal))
            {
                return;
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, cancellationToken);
    }
}
