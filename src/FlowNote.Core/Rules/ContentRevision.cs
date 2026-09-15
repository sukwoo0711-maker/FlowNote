using System.Security.Cryptography;
using System.Text;

namespace FlowNote.Core.Rules;

public static class ContentRevision
{
    public static string Sha256Hex(string? text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text ?? ""));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
