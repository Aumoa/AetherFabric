namespace Aether.BuildTool;

internal sealed class CommandArguments
{
    private readonly Dictionary<string, string?> _options;

    private CommandArguments(string command, Dictionary<string, string?> options)
    {
        Command = command;
        _options = options;
    }

    public string Command { get; }

    public static CommandArguments Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new CommandArguments("help", new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase));
        }

        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < args.Length; index++)
        {
            var token = args[index];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new BuildToolException($"Unexpected argument '{token}'. Options must start with '--'.");
            }

            var name = token[2..];
            string? value = null;
            if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++index];
            }

            if (!options.TryAdd(name, value))
            {
                throw new BuildToolException($"Option '--{name}' was specified more than once.");
            }
        }

        return new CommandArguments(args[0], options);
    }

    public string? Get(string name) => _options.GetValueOrDefault(name);

    public string GetOrDefault(string name, string defaultValue) => Get(name) ?? defaultValue;

    public bool Has(string name) => _options.ContainsKey(name);
}
