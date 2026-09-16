using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using FlowNote.Core.Assist;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Desktop.Commands;

namespace FlowNote.Desktop.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    public MainViewModel(AppSession session)
    {
        Session = session;
        PreviousDayCommand = new RelayCommand(() => Session.SelectDate(Session.SelectedDate.AddDays(-1)));
        NextDayCommand = new RelayCommand(() => Session.SelectDate(Session.SelectedDate.AddDays(1)));
        TodayCommand = new RelayCommand(() =>
            Session.SelectDate(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Session.Database.DisplayTimeZone.TimeZone).DateTime)));
        ChronologicalCommand = new RelayCommand(() => Session.SetViewMode(TimelineViewMode.Chronological));
        ByWorkCommand = new RelayCommand(() => Session.SetViewMode(TimelineViewMode.ByWork));
        PanoramaCommand = new RelayCommand(() => Session.SetViewMode(TimelineViewMode.Panorama));
        OpenPanoramaNoteCommand = new RelayCommandParam(parameter =>
        {
            if (parameter is TimelineRow row)
            {
                Select(row);
            }
        });
        TogglePanoramaSegmentCommand = new RelayCommandParam(parameter =>
        {
            if (parameter is PanoramaSegmentRow segment)
            {
                segment.IsExpanded = !segment.IsExpanded;
            }
        });
        SearchCommand = new RelayCommand(RunSearch);
        DismissOnboardingCommand = new RelayCommand(session.DismissOnboarding);
        CloseDetailCommand = new RelayCommand(CloseDetail);
        OpenFlowCommand = new RelayCommand(() => SetPage(MainPage.Flow));
        OpenTodosCommand = new RelayCommand(() => SetPage(MainPage.Todos));
        OpenSettingsCommand = new RelayCommand(() => SetPage(MainPage.Settings));
        OpenDayReportCommand = new RelayCommand(() => DayReportRequested?.Invoke(null));
        OpenStorageFolderCommand = new RelayCommand(OpenStorageFolder);
        ToggleContinueCommand = new RelayCommand(() => ShowContinuePopup = !ShowContinuePopup);
        OpenContinueWorkCommand = new RelayCommandParam(parameter =>
        {
            if (parameter is ContinueWorkItem item)
            {
                OpenWork(item.WorkItem.Id);
            }
        });
        ContinueRecordingCommand = new RelayCommand(() =>
        {
            if (Session.SelectedWorkItemId is { } id)
            {
                ContinueRecordingRequested?.Invoke(id);
            }
        });
        AddToReportCommand = new RelayCommand(() => DayReportRequested?.Invoke(Session.SelectedWorkItemId));
        EditNextActionCommand = new RelayCommand(() =>
        {
            ShowNextActionEditor = true;
            ShowNextActionEmpty = false;
            Raise(nameof(ShowNextActionEditor));
            Raise(nameof(ShowNextActionEmpty));
        });
        CancelNextActionCommand = new RelayCommand(() =>
        {
            ShowNextActionEditor = false;
            ShowNextActionEmpty = !ShowNextActionText;
            Raise(nameof(ShowNextActionEditor));
            Raise(nameof(ShowNextActionEmpty));
        });
        SaveNextActionCommand = new AsyncRelayCommand(SaveNextActionAsync);
        ClearNextActionCommand = new AsyncRelayCommand(ClearNextActionAsync);
        RestoreNextActionCommand = new AsyncRelayCommand(RestoreNextActionAsync);
        OpenLatestNoteCommand = new RelayCommand(OpenLatestNote);
        OpenWorkFlowCommand = new RelayCommand(OpenWorkFlow);
        CompleteFromContextCommand = new AsyncRelayCommand(() => MutateContextAsync(WorkItemCommand.Complete));
        ReopenFromContextCommand = new AsyncRelayCommand(() => MutateContextAsync(WorkItemCommand.Reopen));
        CancelFromContextCommand = new AsyncRelayCommand(() => MutateContextAsync(WorkItemCommand.Cancel));
        FirstRecordCommand = new RelayCommand(() => FocusCapsuleRequested?.Invoke());
        OpenWorkBadgeCommand = new RelayCommandParam(parameter =>
        {
            if (parameter is TimelineRow { WorkItemId: { } id })
            {
                OpenWork(id);
            }
        });
        LinkDetailWorkCommand = new RelayCommand(() => LinkWorkRequested?.Invoke(Session.SelectedEntryId));
        MarkNextActionFromDetailCommand = new AsyncRelayCommand(MarkNextActionFromDetailAsync);
        SetAssistOffCommand = new RelayCommand(() =>
        {
            Session.Database.Assist.SetMode(AssistMode.Off);
            Session.Engine?.Disable();
            AssistEngineRequested?.Invoke();
            Session.NotifyDataChanged();
        });
        SetAssistRulesCommand = new RelayCommand(() =>
        {
            Session.Database.Assist.SetMode(AssistMode.RulesOnly);
            AssistEngineRequested?.Invoke();
            Session.NotifyDataChanged();
        });
        SetAssistLocalCommand = new RelayCommand(() =>
        {
            if (!Session.Database.Assist.ScopeAcknowledged())
            {
                AssistScopeAckRequested?.Invoke();
                return;
            }

            Session.Database.Assist.SetMode(AssistMode.LocalAssist);
            AssistEngineRequested?.Invoke();
            Session.NotifyDataChanged();
        });
        ClearAssistCommand = new RelayCommand(ClearAssist);
        NewAssistThreadCommand = new RelayCommand(NewAssistThread);
        AssignAssistThreadCommand = new RelayCommandParam(parameter =>
        {
            if (parameter is ContextThread thread)
            {
                AssignAssist(thread.Id);
            }
        });
        UndoAssistCommand = new RelayCommand(UndoAssist);
        ImportModelCommand = new RelayCommand(() => ImportModelRequested?.Invoke());
        RetryEngineCommand = new RelayCommand(() =>
        {
            _ = Session.Engine?.ResetForRetryAsync();
            Session.NotifyDataChanged();
        });
        ContinueWithoutAiCommand = new RelayCommand(() =>
        {
            Session.Database.Assist.SetMode(AssistMode.Off);
            Session.Engine?.Disable();
            AssistEngineRequested?.Invoke();
            Session.NotifyDataChanged();
        });
        session.DataChanged += Reload;
        session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(AppSession.ShowOnboarding))
            {
                Raise(nameof(ShowOnboarding));
            }

            if (args.PropertyName is nameof(AppSession.ViewMode))
            {
                Reload();
            }
        };
        Reload();
    }

    public AppSession Session { get; }

    public ObservableCollection<TimelineRow> Rows { get; } = [];

    public string HeaderTitle => IsToday
        ? "오늘의 흐름"
        : Session.SelectedDate.ToString("M월 d일의 흐름");

    public bool IsToday
    {
        get
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Session.Database.DisplayTimeZone.TimeZone).DateTime);
            return Session.SelectedDate == today;
        }
    }

    public bool ShowOnboarding => Session.ShowOnboarding;

    public string DateLabel => Session.SelectedDate.ToString("yyyy년 M월 d일");

    public string DateHeading => Session.SelectedDate.ToString("M월 d일");

    public bool HasMinimap => MinimapLanes.Count > 0;

    public string SummaryText { get; private set; } = "기록 0개 · 완료 업무 0개 · 첨부 0개";

    public string EmptyText { get; private set; } = "아직 기록이 없습니다";

    public bool HasRows { get; private set; }

    public bool HasDetail { get; private set; }

    public bool IsNarrow { get; private set; }

    public bool IsChronological => Session.ViewMode == TimelineViewMode.Chronological;

    public bool IsByWork => Session.ViewMode == TimelineViewMode.ByWork;

    public bool IsPanorama => Session.ViewMode == TimelineViewMode.Panorama;

    public bool HasDayEntries { get; private set; }

    public bool ShowTimeline => IsFlow && !IsPanorama && HasRows;

    public bool ShowPanorama => IsFlow && IsPanorama && HasPanorama;

    public bool ShowPanoramaHint => IsFlow && IsPanorama && HasDayEntries && !HasPanorama;

    public bool HasUnclassifiedPanorama => PanoramaSegments.Any(static item => item.IsUnclassified);

    public bool ShowEmpty => IsFlow && !HasDayEntries;

    public bool ShowDetailColumn => HasDetail && !IsNarrow;

    public bool ShowDetailOverlay => HasDetail && IsNarrow;

    public bool IsFlow => _page == MainPage.Flow;

    public bool IsTodosPage => _page == MainPage.Todos;

    public bool IsSettingsPage => _page == MainPage.Settings;

    public bool ShowWorkContext => HasDetail && _showWorkContext;

    public bool ShowEntryDetail => HasDetail && !_showWorkContext;

    private bool _showContinuePopup;

    public bool ShowContinuePopup
    {
        get => _showContinuePopup;
        set
        {
            if (_showContinuePopup == value)
            {
                return;
            }

            _showContinuePopup = value;
            Raise(nameof(ShowContinuePopup));
        }
    }

    public string ContinueLabel { get; private set; } = "이어서 할 일 0";

    public ObservableCollection<ContinueWorkItem> ContinuePreview { get; } = [];

    public ObservableCollection<ContinueWorkItem> AllOpenWork { get; } = [];

    public string ContextTitle { get; private set; } = "";

    public string ContextMeta { get; private set; } = "";

    public string ContextNextAction { get; private set; } = "";

    public bool ShowNextActionText { get; private set; }

    public bool ShowNextActionEmpty { get; private set; }

    public bool ShowNextActionEditor { get; private set; }

    public bool ContextSourceUnavailable { get; private set; }

    public bool ShowRestoreNextAction { get; private set; }

    public string NextActionDraft { get; set; } = "";

    public string NextActionCount => $"{NextActionDraft.Length}/{AppLimits.MaxNextActionLength}";

    public string LatestNoteWhen { get; private set; } = "";

    public string LatestNotePreview { get; private set; } = "아직 이 업무에 남긴 기록이 없어요";

    public bool HasLatestNote { get; private set; }

    public bool HasRelatedFiles { get; private set; }

    public ObservableCollection<string> RelatedFileNames { get; } = [];

    public ObservableCollection<string> RecentNoteLines { get; } = [];

    public string AppVersionText => "FlowNote " +
        (typeof(MainViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion
            ?? typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? "unknown");

    public string SettingsPath => Session.Database.Paths.Root;

    public string SettingsMode => Session.IsDemo ? "데모 저장소" : "이 PC의 일반 저장소";

    public string AssistModeText { get; private set; } = "규칙만";

    public string AssistModelText { get; private set; } = "외부 AI 없음 · 기본은 규칙 연결";

    public string CaptureHotkeyText { get; private set; } = "기록 창 단축키는 Ctrl+Alt+Space입니다.";

    public string MinimapLegend { get; private set; } = "기록 기반 연결 · 실작업시간 아님";

    public ObservableCollection<MinimapLane> MinimapLanes { get; } = [];

    public string AssistDetailText { get; private set; } = "";

    public bool ShowAssistDetail => !string.IsNullOrWhiteSpace(AssistDetailText);

    public ObservableCollection<ContextThread> AssistThreadChoices { get; } = [];

    public bool IsNavRail { get; private set; }

    public string EmptyPrimary => EmptyText;

    private MainPage _page = MainPage.Flow;
    private bool _showWorkContext;
    private long _contextGeneration;
    private string? _latestNoteId;

    public string PanoramaHint => HasPanorama
        ? ""
        : "이 날짜에 이을 수 있는 업무 구간이 없습니다. 미연결 기록은 아래에 남습니다.";

    public ObservableCollection<PanoramaSegmentRow> PanoramaSegments { get; } = [];

    public bool HasPanorama => PanoramaSegments.Count > 0;

    public string PanoramaSummary { get; private set; } = "";

    public bool ShowPanoramaSummary => IsPanorama && !string.IsNullOrWhiteSpace(PanoramaSummary);

    public string SearchText { get; set; } = "";

    public string DetailTitle { get; private set; } = "기록을 선택하세요";

    public string DetailBody { get; private set; } = "타임라인에서 메모나 완료 이력을 고르면 원문과 근거가 여기에 나타납니다.";

    public string DetailTimes { get; private set; } = "";

    public string DetailAttachments { get; private set; } = "";

    public string? DetailImagePath { get; private set; }

    public ICommand PreviousDayCommand { get; }
    public ICommand NextDayCommand { get; }
    public ICommand TodayCommand { get; }
    public ICommand ChronologicalCommand { get; }
    public ICommand ByWorkCommand { get; }
    public ICommand PanoramaCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand DismissOnboardingCommand { get; }
    public ICommand CloseDetailCommand { get; }
    public ICommand OpenFlowCommand { get; }
    public ICommand OpenTodosCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand OpenDayReportCommand { get; }
    public ICommand OpenPanoramaNoteCommand { get; }
    public ICommand TogglePanoramaSegmentCommand { get; }
    public ICommand ToggleContinueCommand { get; }
    public ICommand OpenContinueWorkCommand { get; }
    public ICommand ContinueRecordingCommand { get; }
    public ICommand AddToReportCommand { get; }
    public ICommand EditNextActionCommand { get; }
    public ICommand CancelNextActionCommand { get; }
    public ICommand SaveNextActionCommand { get; }
    public ICommand ClearNextActionCommand { get; }
    public ICommand RestoreNextActionCommand { get; }
    public ICommand OpenLatestNoteCommand { get; }
    public ICommand OpenWorkFlowCommand { get; }
    public ICommand CompleteFromContextCommand { get; }
    public ICommand ReopenFromContextCommand { get; }
    public ICommand CancelFromContextCommand { get; }
    public ICommand FirstRecordCommand { get; }
    public ICommand OpenWorkBadgeCommand { get; }
    public ICommand LinkDetailWorkCommand { get; }
    public ICommand MarkNextActionFromDetailCommand { get; }
    public ICommand SetAssistOffCommand { get; }
    public ICommand SetAssistRulesCommand { get; }
    public ICommand SetAssistLocalCommand { get; }
    public ICommand ClearAssistCommand { get; }
    public ICommand NewAssistThreadCommand { get; }
    public ICommand AssignAssistThreadCommand { get; }
    public ICommand UndoAssistCommand { get; }
    public ICommand ImportModelCommand { get; }
    public ICommand RetryEngineCommand { get; }
    public ICommand ContinueWithoutAiCommand { get; }
    public ICommand OpenStorageFolderCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<string?>? DayReportRequested;

    public event Action<string>? ContinueRecordingRequested;

    public event Action? FocusCapsuleRequested;

    public event Action<string?>? LinkWorkRequested;

    public event Action? AssistScopeAckRequested;

    public event Action? AssistEngineRequested;

    public event Action? ImportModelRequested;

    private void OpenStorageFolder()
    {
        var root = Session.Database.Paths.Root;
        if (!Directory.Exists(root))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = root,
            UseShellExecute = true
        });
    }

    public void SetNarrow(bool isNarrow)
    {
        if (IsNarrow == isNarrow)
        {
            return;
        }

        IsNarrow = isNarrow;
        Raise(nameof(IsNarrow));
        Raise(nameof(ShowDetailColumn));
        Raise(nameof(ShowDetailOverlay));
    }

    public void SetNavRail(bool rail)
    {
        if (IsNavRail == rail)
        {
            return;
        }

        IsNavRail = rail;
        Raise(nameof(IsNavRail));
    }

    public void Select(TimelineRow? row)
    {
        if (row is { IsGroup: true })
        {
            return;
        }

        Session.SelectEntry(row?.Id);
        _showWorkContext = false;
        if (row is null)
        {
            HasDetail = false;
            DetailTitle = "기록을 선택하세요";
            DetailBody = "타임라인에서 메모나 완료 이력을 고르면 원문과 근거가 여기에 나타납니다.";
            DetailTimes = "";
            DetailAttachments = "";
            DetailImagePath = null;
            AssistDetailText = "";
        }
        else
        {
            HasDetail = true;
            DetailTitle = row.KindLabel + " · " + row.TimeLabel;
            DetailBody = string.IsNullOrWhiteSpace(row.FullBody) ? row.Title : row.FullBody;
            DetailTimes = row.TimeDetail;
            DetailAttachments = row.AttachmentSummary;
            DetailImagePath = row.ImagePath;
            AssistDetailText = row.AssistDetailText;
            ReloadAssistChoices();
        }

        Raise(nameof(HasDetail));
        Raise(nameof(ShowDetailColumn));
        Raise(nameof(ShowDetailOverlay));
        Raise(nameof(ShowWorkContext));
        Raise(nameof(ShowEntryDetail));
        Raise(nameof(DetailTitle));
        Raise(nameof(DetailBody));
        Raise(nameof(DetailTimes));
        Raise(nameof(DetailAttachments));
        Raise(nameof(DetailImagePath));
        Raise(nameof(AssistDetailText));
        Raise(nameof(ShowAssistDetail));
    }

    public void CloseDetail()
    {
        Session.SelectWork(null);
        Select(null);
    }

    public void Reload()
    {
        var entries = Session.Database.Entries.ListForLocalDateAsync(Session.SelectedDate).GetAwaiter().GetResult();
        var summary = Session.Summarize(entries);
        SummaryText = $"기록 {summary.EntryCount}개 · 완료 업무 {summary.CompletedWorkItemCount}개 · 첨부 {summary.AttachmentCount}개";
        var assignments = Session.Database.Assist.ListAssignments(entries.Select(static item => item.Id).ToList());
        var threadIds = assignments.Values.Where(static item => item.ThreadId is not null).Select(static item => item.ThreadId!);
        var threads = Session.Database.Assist.ListThreadsById(threadIds);
        var dayJobs = Session.Database.Assist.LatestJobs(entries.Select(static item => item.Id).ToList());
        ReloadMinimap(entries, assignments, threads);
        ReloadPanorama(entries, assignments, threads);
        ReloadAssistSettings();
        Rows.Clear();
        if (Session.ViewMode != TimelineViewMode.Panorama)
        {
            IEnumerable<TimelineEntry> ordered = entries;
            if (Session.ViewMode == TimelineViewMode.ByWork)
            {
                ordered = entries
                    .OrderBy(static item => item.WorkItemId is null ? 1 : 0)
                    .ThenBy(static item => item.WorkItemId)
                    .ThenBy(static item => item.OccurredAtUtc)
                    .ThenBy(static item => item.Seq);
            }

            string? lastGroup = null;
            foreach (var entry in ordered)
            {
                if (Session.ViewMode == TimelineViewMode.ByWork)
                {
                    var group = "연결하지 않은 기록";
                    if (!string.IsNullOrEmpty(entry.WorkItemId))
                    {
                        var work = Session.Database.WorkItems.GetAsync(entry.WorkItemId).GetAwaiter().GetResult();
                        group = work?.Title ?? entry.TitleSnapshot ?? "업무";
                    }

                    if (group != lastGroup)
                    {
                        Rows.Add(TimelineRow.Group(group));
                        lastGroup = group;
                    }
                }

                assignments.TryGetValue(entry.Id, out var assignment);
                dayJobs.TryGetValue(entry.Id, out var job);
                ContextThread? thread = null;
                if (assignment?.ThreadId is not null)
                {
                    threads.TryGetValue(assignment.ThreadId, out thread);
                }

                Rows.Add(TimelineRow.From(entry, Session, assignment, thread, job));
            }
        }

        HasDayEntries = entries.Count > 0;
        HasRows = Session.ViewMode == TimelineViewMode.Panorama ? HasDayEntries : Rows.Count > 0;
        EmptyText = HasDayEntries ? "" : "아직 기록이 없습니다";
        Raise(nameof(DateLabel));
        Raise(nameof(DateHeading));
        Raise(nameof(HeaderTitle));
        Raise(nameof(SummaryText));
        Raise(nameof(HasRows));
        Raise(nameof(HasDayEntries));
        Raise(nameof(EmptyText));
        Raise(nameof(ShowTimeline));
        Raise(nameof(ShowEmpty));
        Raise(nameof(ShowPanorama));
        Raise(nameof(ShowPanoramaHint));
        Raise(nameof(ShowPanoramaSummary));
        Raise(nameof(HasPanorama));
        Raise(nameof(HasUnclassifiedPanorama));
        Raise(nameof(PanoramaHint));
        Raise(nameof(PanoramaSummary));
        RaiseViewFlags();
        Raise(nameof(Session));
        ReloadContinue();
        if (!string.IsNullOrEmpty(Session.SelectedWorkItemId))
        {
            OpenWork(Session.SelectedWorkItemId);
        }
        if (!string.IsNullOrEmpty(Session.SelectedEntryId))
        {
            var selected = FindRow(Session.SelectedEntryId);
            if (selected is not null)
            {
                Select(selected);
            }
            else
            {
                HasDetail = false;
                Raise(nameof(HasDetail));
                Raise(nameof(ShowDetailColumn));
                Raise(nameof(ShowDetailOverlay));
            }
        }
        else if (HasDetail)
        {
            CloseDetail();
        }
    }

    private void RunSearch()
    {
        SetPage(MainPage.Flow);
        CloseDetail();
        if (Session.ViewMode != TimelineViewMode.Chronological)
        {
            Session.SetViewMode(TimelineViewMode.Chronological);
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            Reload();
            return;
        }

        var found = Session.Database.Entries.Search(SearchText);
        MinimapLanes.Clear();
        Raise(nameof(HasMinimap));
        Rows.Clear();
        foreach (var entry in found)
        {
            Rows.Add(TimelineRow.From(entry, Session));
        }

        HasRows = Rows.Count > 0;
        EmptyText = HasRows ? "" : "검색 결과가 없습니다";
        HasDayEntries = HasRows;
        SummaryText = $"전체 기록에서 검색 · {Rows.Count}개";
        Raise(nameof(HasDayEntries));
        Raise(nameof(EmptyPrimary));
        Raise(nameof(SummaryText));
        Raise(nameof(HasRows));
        Raise(nameof(EmptyText));
        Raise(nameof(ShowTimeline));
        Raise(nameof(ShowEmpty));
    }

    private void RaiseViewFlags()
    {
        Raise(nameof(IsChronological));
        Raise(nameof(IsByWork));
        Raise(nameof(IsPanorama));
        Raise(nameof(ShowTimeline));
        Raise(nameof(ShowEmpty));
        Raise(nameof(ShowPanorama));
        Raise(nameof(ShowPanoramaHint));
        Raise(nameof(ShowPanoramaSummary));
        Raise(nameof(HasPanorama));
        Raise(nameof(HasUnclassifiedPanorama));
        Raise(nameof(PanoramaHint));
        Raise(nameof(IsFlow));
    }

    private void SetPage(MainPage page)
    {
        _page = page;
        ShowContinuePopup = false;
        Raise(nameof(IsFlow));
        Raise(nameof(IsTodosPage));
        Raise(nameof(IsSettingsPage));
        Raise(nameof(ShowTimeline));
        Raise(nameof(ShowEmpty));
        Raise(nameof(ShowPanorama));
        Raise(nameof(ShowPanoramaHint));
        Raise(nameof(ShowPanoramaSummary));
    }

    public void OpenWork(string workItemId)
    {
        var generation = ++_contextGeneration;
        Session.SelectWork(workItemId);
        var snapshot = Session.Database.WorkContext.Get(workItemId, generation);
        if (snapshot.RequestGeneration != _contextGeneration)
        {
            return;
        }

        _showWorkContext = true;
        HasDetail = true;
        ContextTitle = snapshot.WorkItem.Title;
        ContextMeta = string.Join(" · ", new[] { snapshot.WorkItem.IssueKey, snapshot.StatusLabel }.Where(static item => !string.IsNullOrWhiteSpace(item)));
        ContextNextAction = snapshot.NextActionText ?? "";
        ShowNextActionText = !string.IsNullOrWhiteSpace(snapshot.NextActionText);
        ShowNextActionEmpty = !ShowNextActionText && !ShowNextActionEditor;
        ContextSourceUnavailable = snapshot.NextActionSourceUnavailable;
        ShowRestoreNextAction = snapshot.WorkItem.Status == WorkItemStatus.Open && !string.IsNullOrWhiteSpace(snapshot.LastClearedNextAction);
        NextActionDraft = snapshot.NextActionText ?? "";
        _latestNoteId = snapshot.LatestNote?.Id;
        HasLatestNote = snapshot.LatestNote is not null;
        if (snapshot.LatestNote is { } latest)
        {
            var local = TimeZoneInfo.ConvertTime(latest.OccurredAtUtc, Session.Database.DisplayTimeZone.TimeZone);
            LatestNoteWhen = local.ToString("M월 d일 HH:mm");
            LatestNotePreview = string.IsNullOrWhiteSpace(latest.Body) ? "본문 없음" : latest.Body;
        }
        else
        {
            LatestNoteWhen = "";
            LatestNotePreview = "아직 이 업무에 남긴 기록이 없어요";
        }

        RelatedFileNames.Clear();
        foreach (var file in snapshot.RelatedAttachments)
        {
            RelatedFileNames.Add(file.Attachment.OriginalName);
        }

        HasRelatedFiles = RelatedFileNames.Count > 0;
        RecentNoteLines.Clear();
        foreach (var entry in snapshot.RecentEntries)
        {
            var local = TimeZoneInfo.ConvertTime(entry.OccurredAtUtc, Session.Database.DisplayTimeZone.TimeZone);
            RecentNoteLines.Add(local.ToString("HH:mm") + " · " + (string.IsNullOrWhiteSpace(entry.TitleSnapshot) ? "기록" : entry.TitleSnapshot));
        }

        Raise(nameof(HasDetail));
        Raise(nameof(ShowDetailColumn));
        Raise(nameof(ShowDetailOverlay));
        Raise(nameof(ShowWorkContext));
        Raise(nameof(ShowEntryDetail));
        Raise(nameof(ContextTitle));
        Raise(nameof(ContextMeta));
        Raise(nameof(ContextNextAction));
        Raise(nameof(ShowNextActionText));
        Raise(nameof(ShowNextActionEmpty));
        Raise(nameof(ContextSourceUnavailable));
        Raise(nameof(ShowRestoreNextAction));
        Raise(nameof(NextActionDraft));
        Raise(nameof(NextActionCount));
        Raise(nameof(LatestNoteWhen));
        Raise(nameof(LatestNotePreview));
        Raise(nameof(HasLatestNote));
        Raise(nameof(HasRelatedFiles));
        ShowContinuePopup = false;
    }

    private void ReloadContinue()
    {
        var all = Session.Database.WorkContext.ListContinue();
        ContinuePreview.Clear();
        AllOpenWork.Clear();
        foreach (var item in all)
        {
            AllOpenWork.Add(item);
        }

        foreach (var item in all.Take(3))
        {
            ContinuePreview.Add(item);
        }

        ContinueLabel = $"이어서 할 일 {all.Count}";
        Raise(nameof(ContinueLabel));
    }

    private async Task SaveNextActionAsync()
    {
        if (Session.SelectedWorkItemId is null)
        {
            return;
        }

        var item = await Session.Database.WorkItems.GetAsync(Session.SelectedWorkItemId);
        if (item is null)
        {
            return;
        }

        await Session.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = item.Id,
            RequestId = Guid.NewGuid().ToString("D"),
            ExpectedVersion = item.Version,
            Text = NextActionDraft,
            Reason = string.IsNullOrWhiteSpace(item.NextActionText) ? NextActionChangeReason.Set : NextActionChangeReason.Update
        });
        ShowNextActionEditor = false;
        Session.NotifyDataChanged();
    }

    private async Task ClearNextActionAsync()
    {
        if (Session.SelectedWorkItemId is null)
        {
            return;
        }

        var item = await Session.Database.WorkItems.GetAsync(Session.SelectedWorkItemId);
        if (item is null)
        {
            return;
        }

        await Session.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = item.Id,
            RequestId = Guid.NewGuid().ToString("D"),
            ExpectedVersion = item.Version,
            Reason = NextActionChangeReason.Clear
        });
        ShowNextActionEditor = false;
        Session.NotifyDataChanged();
    }

    private async Task RestoreNextActionAsync()
    {
        if (Session.SelectedWorkItemId is null)
        {
            return;
        }

        var history = Session.Database.WorkItems.ListNextActionHistory(Session.SelectedWorkItemId);
        var text = NextActionRules.LastClearedText(history);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var item = await Session.Database.WorkItems.GetAsync(Session.SelectedWorkItemId);
        if (item is null)
        {
            return;
        }

        await Session.Database.WorkItems.SetNextActionAsync(new NextActionRequest
        {
            WorkItemId = item.Id,
            RequestId = Guid.NewGuid().ToString("D"),
            ExpectedVersion = item.Version,
            Text = text,
            Reason = NextActionChangeReason.Restore
        });
        Session.NotifyDataChanged();
    }

    private void OpenLatestNote()
    {
        if (_latestNoteId is null)
        {
            return;
        }

        var match = Rows.FirstOrDefault(item => item.Id == _latestNoteId);
        if (match is not null)
        {
            Select(match);
            return;
        }

        var entry = Session.Database.Entries.GetByIdAsync(_latestNoteId).GetAwaiter().GetResult();
        if (entry is not null)
        {
            Session.SelectDate(Session.Database.DisplayTimeZone.GetLocalDate(entry.OccurredAtUtc));
        }
    }

    private void OpenWorkFlow()
    {
        if (Session.SelectedWorkItemId is null)
        {
            return;
        }

        Session.SetViewMode(TimelineViewMode.ByWork);
        SetPage(MainPage.Flow);
    }

    private async Task MutateContextAsync(WorkItemCommand command)
    {
        if (Session.SelectedWorkItemId is null)
        {
            return;
        }

        var item = await Session.Database.WorkItems.GetAsync(Session.SelectedWorkItemId);
        if (item is null)
        {
            return;
        }

        var request = new WorkItemCommandRequest
        {
            WorkItemId = item.Id,
            RequestId = Guid.NewGuid().ToString("D"),
            ExpectedVersion = item.Version
        };
        _ = command switch
        {
            WorkItemCommand.Complete => await Session.Database.WorkItems.CompleteAsync(request),
            WorkItemCommand.Reopen => await Session.Database.WorkItems.ReopenAsync(request),
            WorkItemCommand.Cancel => await Session.Database.WorkItems.CancelAsync(request),
            _ => null
        };
        Session.NotifyDataChanged();
    }

    private async Task MarkNextActionFromDetailAsync()
    {
        if (Session.SelectedEntryId is null)
        {
            return;
        }

        var entry = await Session.Database.Entries.GetByIdAsync(Session.SelectedEntryId);
        if (entry is null || entry.Kind != EntryKind.Note)
        {
            return;
        }

        if (entry.WorkItemId is null)
        {
            LinkWorkRequested?.Invoke(entry.Id);
            return;
        }

        NextActionDraft = entry.Body;
        ShowNextActionEditor = true;
        OpenWork(entry.WorkItemId);
        Raise(nameof(NextActionDraft));
        Raise(nameof(ShowNextActionEditor));
    }

    private void ReloadMinimap(
        IReadOnlyList<TimelineEntry> entries,
        IReadOnlyDictionary<string, EntryContextAssignment> assignments,
        IReadOnlyDictionary<string, ContextThread> threads)
    {
        var projection = DayFlowProjector.Project(entries, assignments, threads);
        MinimapLegend = projection.Legend;
        MinimapLanes.Clear();
        var byId = entries.ToDictionary(static item => item.Id, StringComparer.Ordinal);
        var zone = Session.Database.DisplayTimeZone.TimeZone;
        var laneIds = new List<string>();
        foreach (var episode in projection.Episodes)
        {
            if (!laneIds.Contains(episode.ThreadId, StringComparer.Ordinal))
            {
                laneIds.Add(episode.ThreadId);
            }
        }

        foreach (var request in projection.RequestMarkers)
        {
            if (!laneIds.Contains(request.ThreadId, StringComparer.Ordinal))
            {
                laneIds.Add(request.ThreadId);
            }
        }

        foreach (var threadId in laneIds)
        {
            if (!threads.TryGetValue(threadId, out var thread))
            {
                continue;
            }

            var nodes = new List<MinimapNode>();
            foreach (var episode in projection.Episodes.Where(item => item.ThreadId == threadId))
            {
                foreach (var id in episode.ObservedEntryIds)
                {
                    if (!byId.TryGetValue(id, out var entry))
                    {
                        continue;
                    }

                    var role = assignments.TryGetValue(id, out var assignment) ? assignment.Role : ContextRole.Unknown;
                    var completion = role == ContextRole.CompletionMention;
                    nodes.Add(new MinimapNode
                    {
                        Text = TimeZoneInfo.ConvertTime(entry.OccurredAtUtc, zone).ToString("HH:mm"),
                        Marker = completion ? "◇" : "●",
                        Kind = completion ? "completion" : "observed"
                    });
                }

                if (episode.HasObservationGap)
                {
                    nodes.Add(new MinimapNode { Text = "관측 공백", Marker = "···", Kind = "gap" });
                }
            }

            foreach (var request in projection.RequestMarkers.Where(item => item.ThreadId == threadId))
            {
                if (!byId.TryGetValue(request.EntryId, out var entry))
                {
                    continue;
                }

                nodes.Add(new MinimapNode
                {
                    Text = TimeZoneInfo.ConvertTime(entry.OccurredAtUtc, zone).ToString("HH:mm") + " 요청",
                    Marker = "▽",
                    Kind = "request"
                });
            }

            MinimapLanes.Add(new MinimapLane { Title = thread.Title, Nodes = nodes });
        }

        Raise(nameof(MinimapLegend));
        Raise(nameof(HasMinimap));
    }

    private void ReloadPanorama(
        IReadOnlyList<TimelineEntry> entries,
        IReadOnlyDictionary<string, EntryContextAssignment> assignments,
        IReadOnlyDictionary<string, ContextThread> threads)
    {
        var expanded = PanoramaSegments
            .Where(static item => item.IsExpanded)
            .Select(static item => item.Key)
            .ToHashSet(StringComparer.Ordinal);
        var mentionMap = Session.Database.Assist.ListMentionsForEntries(entries.Select(static item => item.Id).ToList());
        var projection = DayFlowProjector.Project(entries, assignments, threads, mentionMap);
        var byId = entries.ToDictionary(static item => item.Id, StringComparer.Ordinal);
        var attachmentCounts = Session.Database.Entries.CountAttachments(entries.Select(static item => item.Id).ToList());
        var jobs = Session.Database.Assist.LatestJobs(entries.Select(static item => item.Id).ToList());
        var staged = new List<(DateTimeOffset Time, long Seq, PanoramaSegmentRow Row)>();

        foreach (var episode in projection.Episodes)
        {
            var observed = episode.ObservedEntryIds
                .Select(id => byId.GetValueOrDefault(id))
                .OfType<TimelineEntry>()
                .OrderBy(static item => item.OccurredAtUtc)
                .ThenBy(static item => item.Seq)
                .ToList();
            if (observed.Count == 0)
            {
                continue;
            }

            var first = observed[0];
            var last = observed[^1];
            var attachmentCount = observed.Sum(item => attachmentCounts.GetValueOrDefault(item.Id));
            var completion = observed.Any(item =>
                assignments.TryGetValue(item.Id, out var assignment)
                && assignment.Role == ContextRole.CompletionMention);
            staged.Add((first.OccurredAtUtc, first.Seq, new PanoramaSegmentRow
            {
                Key = "episode:" + episode.ThreadId + ":" + first.Id,
                Kind = PanoramaSegmentKind.Episode,
                Title = episode.Title,
                TimeLabel = FormatObservedRange(first.OccurredAtUtc, last.OccurredAtUtc),
                CountLabel = FormatCounts(observed.Count, attachmentCount),
                StatusLabel = completion
                    ? "완료 언급"
                    : episode.HasObservationGap ? "관측 공백" : "",
                ThreadId = episode.ThreadId,
                AccentIndex = AccentFor(episode.ThreadId),
                HasObservationGap = episode.HasObservationGap,
                IsExpanded = expanded.Contains("episode:" + episode.ThreadId + ":" + first.Id),
                Notes = observed.Select(item =>
                {
                    assignments.TryGetValue(item.Id, out var assignment);
                    threads.TryGetValue(assignment?.ThreadId ?? "", out var thread);
                    jobs.TryGetValue(item.Id, out var job);
                    return TimelineRow.From(item, Session, assignment, thread, job);
                }).ToList()
            }));
        }

        foreach (var request in projection.RequestMarkers)
        {
            if (!byId.TryGetValue(request.EntryId, out var entry))
            {
                continue;
            }

            var attachments = attachmentCounts.GetValueOrDefault(entry.Id);
            assignments.TryGetValue(entry.Id, out var assignment);
            threads.TryGetValue(request.ThreadId, out var thread);
            jobs.TryGetValue(entry.Id, out var requestJob);
            var key = "request:" + request.EntryId + ":" + request.ThreadId;
            staged.Add((entry.OccurredAtUtc, entry.Seq, new PanoramaSegmentRow
            {
                Key = key,
                Kind = PanoramaSegmentKind.Request,
                Title = request.Title,
                TimeLabel = FormatObservedRange(entry.OccurredAtUtc, entry.OccurredAtUtc),
                CountLabel = FormatCounts(1, attachments),
                StatusLabel = AssistCodec.RoleLabel(request.Role),
                ThreadId = request.ThreadId,
                AccentIndex = AccentFor(request.ThreadId),
                IsExpanded = expanded.Contains(key),
                Notes = [TimelineRow.From(entry, Session, assignment, thread, requestJob)]
            }));
        }

        foreach (var marker in projection.OfficialMarkers)
        {
            if (!byId.TryGetValue(marker.EntryId, out var entry))
            {
                continue;
            }

            var key = "official:" + marker.EntryId;
            assignments.TryGetValue(entry.Id, out var assignment);
            threads.TryGetValue(marker.ThreadId, out var thread);
            jobs.TryGetValue(entry.Id, out var officialJob);
            staged.Add((entry.OccurredAtUtc, entry.Seq, new PanoramaSegmentRow
            {
                Key = key,
                Kind = PanoramaSegmentKind.Official,
                Title = marker.Title,
                TimeLabel = FormatObservedRange(entry.OccurredAtUtc, entry.OccurredAtUtc),
                CountLabel = entry.TitleSnapshot ?? "",
                StatusLabel = marker.Kind == EntryKind.TaskCompleted ? "공식 완료" : "공식 취소",
                ThreadId = marker.ThreadId,
                AccentIndex = string.IsNullOrEmpty(marker.ThreadId) ? 0 : AccentFor(marker.ThreadId),
                IsExpanded = expanded.Contains(key),
                Notes = [TimelineRow.From(entry, Session, assignment, thread, officialJob)]
            }));
        }

        var unclassified = projection.UnclassifiedEntryIds
            .Select(id => byId.GetValueOrDefault(id))
            .OfType<TimelineEntry>()
            .OrderBy(static item => item.OccurredAtUtc)
            .ThenBy(static item => item.Seq)
            .ToList();
        if (unclassified.Count > 0)
        {
            var first = unclassified[0];
            const string key = "unclassified";
            staged.Add((first.OccurredAtUtc, first.Seq, new PanoramaSegmentRow
            {
                Key = key,
                Kind = PanoramaSegmentKind.Unclassified,
                Title = "미연결 기록",
                TimeLabel = FormatObservedRange(first.OccurredAtUtc, unclassified[^1].OccurredAtUtc),
                CountLabel = FormatCounts(unclassified.Count, unclassified.Sum(item => Session.Database.Entries.ListAttachments(item.Id).Count)),
                StatusLabel = "단서 부족",
                ThreadId = "",
                AccentIndex = 0,
                IsExpanded = expanded.Contains(key),
                Notes = unclassified.Select(item =>
                {
                    assignments.TryGetValue(item.Id, out var assignment);
                    jobs.TryGetValue(item.Id, out var unclassifiedJob);
                    return TimelineRow.From(item, Session, assignment, null, unclassifiedJob);
                }).ToList()
            }));
        }

        PanoramaSegments.Clear();
        var ordered = staged.OrderBy(static item => item.Time).ThenBy(static item => item.Seq).ToList();
        var returns = DayFlowReadModel.MarkSameWorkReturns(
            ordered.Select(static item => (item.Row.IsEpisode, item.Row.ThreadId)).ToList());
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Row.IsEpisode)
            {
                ordered[i].Row.ShowsSameWorkReturn = returns[i];
            }

            PanoramaSegments.Add(ordered[i].Row);
        }

        PanoramaSummary = string.Join(
            " → ",
            PanoramaSegments.Where(static item => item.IsEpisode).Select(static item => item.Title));
    }

    private TimelineRow? FindRow(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return Rows.FirstOrDefault(item => item.Id == id)
            ?? PanoramaSegments.SelectMany(static item => item.Notes).FirstOrDefault(item => item.Id == id);
    }

    private string FormatObservedRange(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        var zone = Session.Database.DisplayTimeZone.TimeZone;
        var start = TimeZoneInfo.ConvertTime(startUtc, zone).ToString("HH:mm");
        var end = TimeZoneInfo.ConvertTime(endUtc, zone).ToString("HH:mm");
        return start == end ? start : start + "~" + end;
    }

    private static string FormatCounts(int notes, int attachments)
    {
        var text = notes + "개 기록";
        return attachments == 0 ? text : text + " · 첨부 " + attachments;
    }

    private static int AccentFor(string threadId)
    {
        unchecked
        {
            var hash = 23;
            foreach (var ch in threadId)
            {
                hash = (hash * 31) + ch;
            }

            return (hash & 0x7FFFFFFF) % 6;
        }
    }

    private void ReloadAssistSettings()
    {
        var settings = Session.Database.Assist.GetSettings();
        AssistModeText = settings.Mode switch
        {
            AssistMode.Off => "끔",
            AssistMode.LocalAssist => "로컬 보조",
            _ => "규칙만"
        };
        AssistModelText = settings.Mode switch
        {
            AssistMode.LocalAssist => "외부 AI 없음 · 동봉 로컬 엔진 · " + (Session.Engine?.StatusText ?? "모델이 없으면 가져오기"),
            AssistMode.Off => "외부 AI 없음 · 연결 분석 끔 · 원문 저장은 그대로 동작합니다",
            _ => "외부 AI 없음 · 기본은 규칙 연결 · 모델 없이 같은 업무를 이습니다"
        };
        Raise(nameof(AssistModeText));
        Raise(nameof(AssistModelText));
    }

    public void SetCaptureHotkeyStatus(bool registered)
    {
        CaptureHotkeyText = registered
            ? "기록 창 단축키는 Ctrl+Alt+Space입니다. 다른 앱을 쓰다가도 바로 한 줄 입력칸으로 옵니다."
            : "Ctrl+Alt+Space는 다른 앱이 쓰고 있어 등록하지 못했습니다. 트레이의 기록 창으로 열 수 있습니다.";
        Raise(nameof(CaptureHotkeyText));
    }

    private void ReloadAssistChoices()
    {
        AssistThreadChoices.Clear();
        foreach (var thread in Session.Database.Assist.ListThreads())
        {
            AssistThreadChoices.Add(thread);
        }
    }

    private void ClearAssist()
    {
        if (Session.SelectedEntryId is null)
        {
            return;
        }

        Session.Database.Assist.CorrectAssignment(Session.SelectedEntryId, Guid.NewGuid().ToString("D"), null, false, null);
        Session.NotifyDataChanged();
    }

    private void NewAssistThread()
    {
        if (Session.SelectedEntryId is null)
        {
            return;
        }

        Session.Database.Assist.CorrectAssignment(Session.SelectedEntryId, Guid.NewGuid().ToString("D"), null, true, null);
        Session.NotifyDataChanged();
    }

    private void AssignAssist(string threadId)
    {
        if (Session.SelectedEntryId is null)
        {
            return;
        }

        Session.Database.Assist.CorrectAssignment(Session.SelectedEntryId, Guid.NewGuid().ToString("D"), threadId, false, null);
        Session.NotifyDataChanged();
    }

    private void UndoAssist()
    {
        if (Session.SelectedEntryId is null)
        {
            return;
        }

        Session.Database.Assist.UndoCorrection(Session.SelectedEntryId, Guid.NewGuid().ToString("D"));
        Session.NotifyDataChanged();
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class TimelineRow
{
    public required string Id { get; init; }
    public required string TimeLabel { get; init; }
    public required string Title { get; init; }
    public required string Preview { get; init; }
    public required string FullBody { get; init; }
    public required string KindLabel { get; init; }
    public required string TimeDetail { get; init; }
    public required string AttachmentSummary { get; init; }
    public string? ImagePath { get; init; }
    public bool IsGroup { get; init; }
    public bool IsEvent { get; init; }
    public bool IsSelected { get; init; }
    public string NodeKind { get; init; } = "note";
    public IReadOnlyList<AttachmentPreview> AttachmentPreviews { get; init; } = [];

    public string? WorkItemId { get; init; }

    public string WorkBadgeText { get; init; } = "";

    public bool ShowWorkBadge => !string.IsNullOrWhiteSpace(WorkBadgeText);

    public string? HeroImagePath { get; init; }

    public bool ShowHeroImage => !string.IsNullOrWhiteSpace(HeroImagePath);

    public IReadOnlyList<AttachmentPreview> FileTiles { get; init; } = [];

    public bool ShowFileTiles => FileTiles.Count > 0;

    public string OverflowText { get; init; } = "";

    public bool ShowOverflow => !string.IsNullOrWhiteSpace(OverflowText);

    public string AssistBadgeText { get; init; } = "";

    public bool ShowAssistBadge => !string.IsNullOrWhiteSpace(AssistBadgeText);

    public string AssistDetailText { get; init; } = "";

    public string ExpandedText => string.IsNullOrWhiteSpace(FullBody) ? Title : FullBody;

    public string DisplayPreview => NoteDisplayRules.DisplayPreview(Title, Preview);

    public bool ShowPreview => !string.IsNullOrWhiteSpace(DisplayPreview);

    public bool ShowKindLabel => NoteDisplayRules.ShowKindChrome(KindLabel, IsEvent, IsGroup);

    public bool ShowAttachmentSummary => AttachmentPreviews.Count > 0;

    public bool ShowOccurredDifference => !string.IsNullOrWhiteSpace(TimeDetail);

    public static TimelineRow Group(string title) => new()
    {
        Id = "",
        TimeLabel = "",
        Title = title,
        Preview = "",
        FullBody = "",
        KindLabel = "그룹",
        TimeDetail = "",
        AttachmentSummary = "",
        IsGroup = true
    };

    public static TimelineRow From(
        TimelineEntry entry,
        AppSession session,
        EntryContextAssignment? assignment = null,
        ContextThread? thread = null,
        AnalysisJob? job = null)
    {
        var local = TimeZoneInfo.ConvertTime(entry.OccurredAtUtc, session.Database.DisplayTimeZone.TimeZone);
        var recordedLocal = TimeZoneInfo.ConvertTime(entry.RecordedAtUtc, session.Database.DisplayTimeZone.TimeZone);
        var kind = TimelineDisplayRules.KindLabel(entry.Kind);
        var attachments = session.Database.Entries.ListAttachments(entry.Id);
        var names = attachments.Count == 0
            ? "첨부 없음"
            : string.Join(", ", attachments.Select(static item => item.OriginalName));
        var missing = attachments.Where(item => !File.Exists(session.Database.Attachments.ResolveFullPath(item))).ToList();
        if (missing.Count > 0)
        {
            names += " · 일부 파일을 찾지 못했습니다";
        }

        var previews = new List<AttachmentPreview>();
        string? imagePath = null;
        foreach (var attachment in attachments)
        {
            var full = session.Database.Attachments.ResolveFullPath(attachment);
            var exists = File.Exists(full);
            var isImage = AttachmentRules.IsImage(attachment.MediaType);
            previews.Add(new AttachmentPreview
            {
                Name = attachment.OriginalName,
                Path = exists ? full : full,
                IsImage = isImage,
                ByteSize = attachment.ByteSize,
                Exists = exists
            });
            if (imagePath is null && isImage && exists)
            {
                imagePath = full;
            }
        }

        var timeDetail = TimelineDisplayRules.TimeDifferenceLabel(local, recordedLocal) ?? "";
        var hasImage = previews.Any(static item => item.IsImage && item.Exists);
        var title = entry.Kind == EntryKind.Note
            ? NoteDisplayRules.NoteCardTitle(entry.TitleSnapshot, entry.Body, hasImage, attachments.Count > 0)
            : string.IsNullOrWhiteSpace(entry.TitleSnapshot) ? kind : entry.TitleSnapshot;
        var preview = string.IsNullOrWhiteSpace(entry.Body) ? "" : TrimPreview(entry.Body);
        var hero = previews.FirstOrDefault(static item => item.IsImage && item.Exists);
        var tiles = hero is null ? previews.Take(3).ToList() : previews.Where(item => item != hero).Take(3).ToList();
        var overflow = hero is null ? previews.Count - tiles.Count : previews.Count - 1 - tiles.Count;
        var badge = "";
        if (entry.WorkItemId is not null)
        {
            var work = session.Database.WorkItems.GetAsync(entry.WorkItemId).GetAwaiter().GetResult();
            badge = FlowNote.Core.Rules.WorkChipText.ForCapsule(work?.IssueKey ?? entry.IssueKey, work?.Title ?? entry.TitleSnapshot ?? "업무");
        }

        return new TimelineRow
        {
            Id = entry.Id,
            TimeLabel = local.ToString("HH:mm"),
            Title = title,
            Preview = preview,
            FullBody = entry.Body,
            KindLabel = kind,
            TimeDetail = timeDetail,
            AttachmentSummary = names,
            ImagePath = imagePath,
            IsEvent = entry.Kind != EntryKind.Note,
            NodeKind = TimelineDisplayRules.NodeKind(entry.Kind),
            AttachmentPreviews = previews,
            IsSelected = entry.Id == session.SelectedEntryId,
            WorkItemId = entry.WorkItemId,
            WorkBadgeText = badge,
            HeroImagePath = hero?.Path,
            FileTiles = tiles,
            OverflowText = overflow > 0 ? $"+{overflow}" : "",
            AssistBadgeText = AssistBadge(assignment, thread, job),
            AssistDetailText = AssistDetail(assignment, thread, job)
        };
    }

    private static string AssistBadge(EntryContextAssignment? assignment, ContextThread? thread, AnalysisJob? job)
    {
        if (assignment is { Resolution: AssignmentResolution.Assigned, ThreadId: not null })
        {
            var title = thread?.Title ?? "묶음";
            return $"{AssistCodec.RoleLabel(assignment.Role)} · {title}";
        }

        if (job is { Status: AnalysisJobStatus.Pending or AnalysisJobStatus.Running or AnalysisJobStatus.RetryWait })
        {
            return "분석 대기";
        }

        if (job is { Status: AnalysisJobStatus.Blocked or AnalysisJobStatus.Failed })
        {
            return "분석 실패";
        }

        return assignment is { Resolution: AssignmentResolution.ManualClear } ? "연결 해제" : "미분류";
    }

    private static string AssistDetail(EntryContextAssignment? assignment, ContextThread? thread, AnalysisJob? job)
    {
        var badge = AssistBadge(assignment, thread, job);
        var origin = assignment is null ? "규칙 미적용" : AssistCodec.OriginLabel(assignment.Origin);
        var quote = string.IsNullOrWhiteSpace(assignment?.SourceQuote) ? "" : " · \"" + assignment.SourceQuote + "\"";
        var jobText = job is null ? "" : " · " + AssistCodec.JobStatusLabel(job.Status);
        return origin + " · " + badge + quote + jobText;
    }

    private static string TrimPreview(string body)
    {
        var normalized = body.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        var lines = normalized.Split('\n');
        var take = string.Join("\n", lines.Take(3));
        return take.Length <= 180 ? take : take[..180] + "…";
    }
}

public enum MainPage
{
    Flow,
    Todos,
    Settings
}

public enum PanoramaSegmentKind
{
    Episode,
    Request,
    Official,
    Unclassified
}

public sealed class PanoramaSegmentRow : INotifyPropertyChanged
{
    private bool _expanded;

    public required string Key { get; init; }

    public required PanoramaSegmentKind Kind { get; init; }

    public required string Title { get; init; }

    public required string TimeLabel { get; init; }

    public required string CountLabel { get; init; }

    public required string StatusLabel { get; init; }

    public required string ThreadId { get; init; }

    public required int AccentIndex { get; init; }

    public bool HasObservationGap { get; init; }

    public bool ShowsSameWorkReturn { get; set; }

    public required IReadOnlyList<TimelineRow> Notes { get; init; }

    public bool IsEpisode => Kind == PanoramaSegmentKind.Episode;

    public bool IsRequest => Kind == PanoramaSegmentKind.Request;

    public bool IsOfficial => Kind == PanoramaSegmentKind.Official;

    public bool IsUnclassified => Kind == PanoramaSegmentKind.Unclassified;

    public bool ShowStatus => !string.IsNullOrWhiteSpace(StatusLabel);

    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (_expanded == value)
            {
                return;
            }

            _expanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class MinimapLane
{
    public required string Title { get; init; }

    public required IReadOnlyList<MinimapNode> Nodes { get; init; }
}

public sealed class MinimapNode
{
    public required string Text { get; init; }

    public required string Kind { get; init; }

    public required string Marker { get; init; }
}
