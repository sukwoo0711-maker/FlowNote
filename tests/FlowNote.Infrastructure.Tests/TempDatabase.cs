using FlowNote.Core.Time;
using FlowNote.Infrastructure;
using FlowNote.Infrastructure.Paths;

namespace FlowNote.Infrastructure.Tests;

internal sealed class TempDatabase : IDisposable
{
    public TempDatabase(IClock? clock = null, DisplayTimeZone? timeZone = null)
    {
        Root = Path.Combine(Path.GetTempPath(), "FlowNoteTests", Guid.NewGuid().ToString("N"));
        Clock = clock ?? new FakeClock(new DateTimeOffset(2026, 9, 14, 1, 24, 0, TimeSpan.Zero));
        TimeZone = timeZone ?? DisplayTimeZone.Korea();
        Paths = AppStoragePaths.Create(AppStorageMode.Live, Root);
        Database = new FlowNoteDatabase(Paths, Clock, TimeZone);
    }

    public string Root { get; }

    public IClock Clock { get; }

    public DisplayTimeZone TimeZone { get; }

    public AppStoragePaths Paths { get; }

    public FlowNoteDatabase Database { get; private set; }

    public FlowNoteDatabase Reopen()
    {
        Database.Dispose();
        Database = new FlowNoteDatabase(Paths, Clock, TimeZone);
        return Database;
    }

    public void Dispose()
    {
        Database.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
