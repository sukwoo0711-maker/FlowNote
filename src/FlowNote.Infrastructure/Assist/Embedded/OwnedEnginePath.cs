namespace FlowNote.Infrastructure.Assist.Embedded;

public static class OwnedEnginePath
{
    public static bool IsAllowed(string appBase, string executable)
    {
        if (string.IsNullOrWhiteSpace(appBase) || string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }

        var root = Path.GetFullPath(appBase);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        var full = Path.GetFullPath(executable);
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var name = Path.GetFileName(full);
        if (!string.Equals(name, "llama-server.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (full.Contains("ggml-rpc", StringComparison.OrdinalIgnoreCase) ||
            full.Contains("rpc-server", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
