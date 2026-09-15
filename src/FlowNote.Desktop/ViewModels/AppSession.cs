using System.ComponentModel;
using System.Globalization;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Infrastructure;

namespace FlowNote.Desktop.ViewModels;

public enum TimelineViewMode
{
    Chronological,
    ByWork,
    Panorama
}

public sealed class AppSession : INotifyPropertyChanged
{
    public AppSession(FlowNoteDatabase database)
    {
        Database = database;
        SelectedDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, database.DisplayTimeZone.TimeZone).DateTime);
        PinFloating = database.Settings.Get("floating.topmost") != "false";
        ShowOnboarding = database.Settings.Get("onboarding.seen") != "true";
        PinnedPreview = ParsePinnedPreview(database.Settings.Get("floating.pinnedPreview"));
        database.Settings.Set("floating.layoutVersion", CapsuleLayout.LayoutVersion.ToString(CultureInfo.InvariantCulture));
    }

    public FlowNoteDatabase Database { get; }

    public FlowNote.Infrastructure.Assist.Embedded.EmbeddedEngineManager? Engine { get; set; }

    public DateOnly SelectedDate { get; private set; }

    public string? SelectedEntryId { get; private set; }

    public string? SelectedWorkItemId { get; private set; }

    public bool IsDemo => Database.Paths.Mode == FlowNote.Infrastructure.Paths.AppStorageMode.Demo;

    public TimelineViewMode ViewMode { get; private set; } = TimelineViewMode.Chronological;

    public bool PinFloating { get; private set; }

    public FloatingPinnedPreview PinnedPreview { get; private set; }

    public bool ShowOnboarding { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? DataChanged;

    public void SelectDate(DateOnly date)
    {
        SelectedDate = date;
        Raise(nameof(SelectedDate));
        DataChanged?.Invoke();
    }

    public void SelectEntry(string? id)
    {
        SelectedEntryId = id;
        if (id is not null)
        {
            SelectedWorkItemId = null;
            Raise(nameof(SelectedWorkItemId));
        }

        Raise(nameof(SelectedEntryId));
    }

    public void SelectWork(string? id)
    {
        SelectedWorkItemId = id;
        if (id is not null)
        {
            SelectedEntryId = null;
            Raise(nameof(SelectedEntryId));
        }

        Raise(nameof(SelectedWorkItemId));
    }

    public void SetViewMode(TimelineViewMode mode)
    {
        ViewMode = mode;
        Raise(nameof(ViewMode));
        DataChanged?.Invoke();
    }

    public void SetPinFloating(bool value)
    {
        PinFloating = value;
        Database.Settings.Set("floating.topmost", value ? "true" : "false");
        Raise(nameof(PinFloating));
    }

    public void SetPinnedPreview(FloatingPinnedPreview value)
    {
        PinnedPreview = value;
        Database.Settings.Set("floating.pinnedPreview", value.ToString().ToLowerInvariant());
        Raise(nameof(PinnedPreview));
    }

    public void SaveFloatingPosition(double left, double top)
    {
        Database.Settings.Set("floating.left", left.ToString("0.###", CultureInfo.InvariantCulture));
        Database.Settings.Set("floating.top", top.ToString("0.###", CultureInfo.InvariantCulture));
    }

    public bool TryReadFloatingPosition(out double left, out double top)
    {
        left = 0;
        top = 0;
        var leftText = Database.Settings.Get("floating.left");
        var topText = Database.Settings.Get("floating.top");
        return leftText is not null
            && topText is not null
            && double.TryParse(leftText, NumberStyles.Float, CultureInfo.InvariantCulture, out left)
            && double.TryParse(topText, NumberStyles.Float, CultureInfo.InvariantCulture, out top);
    }

    private static FloatingPinnedPreview ParsePinnedPreview(string? value)
    {
        return value switch
        {
            "todo" => FloatingPinnedPreview.Todo,
            "recent" => FloatingPinnedPreview.Recent,
            _ => FloatingPinnedPreview.None
        };
    }

    public void DismissOnboarding()
    {
        ShowOnboarding = false;
        Database.Settings.Set("onboarding.seen", "true");
        Raise(nameof(ShowOnboarding));
    }

    public void NotifyDataChanged()
    {
        DataChanged?.Invoke();
    }

    public DaySummary Summarize(IReadOnlyList<TimelineEntry> dayEntries)
    {
        var asOf = SelectedDate == DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Database.DisplayTimeZone.TimeZone).DateTime)
            ? DateTimeOffset.UtcNow
            : Database.DisplayTimeZone.GetUtcRange(SelectedDate).ExclusiveEndUtc;
        var lifecycle = Database.Entries.ListLifecycleThrough(asOf);
        var summary = DayAggregator.Summarize(dayEntries, lifecycle, SelectedDate, Database.DisplayTimeZone);
        var attachmentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in dayEntries)
        {
            foreach (var attachment in Database.Entries.ListAttachments(entry.Id))
            {
                attachmentIds.Add(attachment.Id);
            }
        }

        return summary with { AttachmentCount = attachmentIds.Count };
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
