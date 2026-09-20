using System.Security.Cryptography;
using System.Text;

namespace Aether.BuildTool;

internal static class DeterministicGuid
{
    private static readonly Guid Namespace = new("927AD672-81A2-49B7-AB47-173BFA6B7DC8");

    public static Guid Create(string value)
    {
        var namespaceBytes = Namespace.ToByteArray();
        var valueBytes = Encoding.UTF8.GetBytes(value.Replace('\\', '/').ToUpperInvariant());
        var data = new byte[namespaceBytes.Length + valueBytes.Length];
        namespaceBytes.CopyTo(data, 0);
        valueBytes.CopyTo(data, namespaceBytes.Length);
        var hash = SHA256.HashData(data);
        return new Guid(hash.AsSpan(0, 16));
    }
}
