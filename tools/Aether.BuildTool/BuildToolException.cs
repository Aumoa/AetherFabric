namespace Aether.BuildTool;

internal sealed class BuildToolException : Exception
{
    public BuildToolException(string message)
        : base(message)
    {
    }

    public BuildToolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
