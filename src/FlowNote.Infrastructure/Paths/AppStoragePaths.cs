namespace FlowNote.Infrastructure.Paths;

public enum AppStorageMode
{
    Live,
    Demo
}

public sealed class AppStoragePaths
{
    private AppStoragePaths(AppStorageMode mode, string root)
    {
        Mode = mode;
        Root = root;
    }

    public AppStorageMode Mode { get; }

    public string Root { get; }

    public string DatabasePath => Path.Combine(Root, "flownote.db");

    public string AttachmentsDirectory => Path.Combine(Root, "attachments");

    public string ThumbnailsDirectory => Path.Combine(Root, "thumbnails");

    public string StagingDirectory => Path.Combine(Root, "staging");

    public static AppStoragePaths Create(AppStorageMode mode, string? localAppDataRoot = null)
    {
        var local = localAppDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = mode == AppStorageMode.Demo ? "demo" : "live";
        var root = Path.Combine(local, "FlowNote", folder);
        var paths = new AppStoragePaths(mode, root);
        paths.EnsureDirectories();
        return paths;
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(AttachmentsDirectory);
        Directory.CreateDirectory(ThumbnailsDirectory);
        Directory.CreateDirectory(StagingDirectory);
    }
}
