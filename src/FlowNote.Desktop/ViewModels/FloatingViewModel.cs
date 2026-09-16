using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Desktop.Commands;
using FlowNote.Desktop.Controls;
using FlowNote.Infrastructure.Drafts;

namespace FlowNote.Desktop.ViewModels;

public sealed class FloatingViewModel : INotifyPropertyChanged, IPendingAttachmentHost
{
    public const string DraftId = "floating-note";

    private string _body = "";
    private bool _saveSucceeded;
    private bool _saveFailed;
    private bool _isAttaching;
    private string? _undoWorkItemId;
    private int _undoVersion;
    private string _undoTitle = "";
    private CapsulePanelKind _panelKind = CapsulePanelKind.None;
    private CapsuleInputMode _inputMode = CapsuleInputMode.SingleLine;
    private string? _successFlash;
    private bool _showAddTodoInput;
    private bool _isDraggingFiles;
    private string? _draftWorkItemId;
    private string? _draftWorkTitle;
    private string? _draftWorkIssueKey;
    private WorkItemStatus? _draftWorkStatus;
    private CapsuleRecentRow? _peekRow;
    private string _pinnedTitle = "";
    private string _pinnedKindLabel = "";
    private string? _submitRequestId;

    public FloatingViewModel(AppSession session)
    {
        Session = session;
        SaveNoteCommand = new AsyncRelayCommand(SaveNoteAsync, () => CanSave);
        AddWorkItemCommand = new AsyncRelayCommand(AddWorkItemAsync, () => CanAddWorkItem);
        TogglePinCommand = new RelayCommand(() => Session.SetPinFloating(!Session.PinFloating));
        UndoCompleteCommand = new AsyncRelayCommand(UndoCompleteAsync, () => CanUndoComplete);
        OpenTodayCommand = new RelayCommand(RequestToday);
        ToggleRecentCommand = new RelayCommand(ToggleBoard);
        ToggleTodoCommand = new RelayCommand(ShowTodosOnBoard);
        ShowRecentCommand = new RelayCommand(ShowRecentOnBoard);
        CollapseBoardCommand = new RelayCommand(CollapseBoard);
        ClosePeekCommand = new RelayCommand(ClosePeek);
        PinPeekCommand = new RelayCommand(PinPeek);
        UnpinBoardCommand = new RelayCommand(UnpinBoard);
        CopyPeekCommand = new RelayCommand(CopyPeek);
        OpenPeekInDayCommand = new RelayCommand(OpenPeekInDay);
        OpenLongNoteCommand = new RelayCommand(OpenLongNote);
        HideCommand = new RelayCommand(() => HideRequested?.Invoke());
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke());
        CaptureRegionCommand = new RelayCommand(() => RegionCaptureRequested?.Invoke());
        AttachCommand = new RelayCommand(() => AttachRequested?.Invoke());
        ToggleAddTodoCommand = new RelayCommand(() =>
        {
            ShowAddTodoInput = !ShowAddTodoInput;
            Raise(nameof(ShowAddTodoInput));
        });
        TogglePinPreviewCommand = new RelayCommand(TogglePinPreview);
        ShowStorageCommand = new RelayCommand(() => StorageRequested?.Invoke());
        PickWorkCommand = new RelayCommand(() => PickWorkRequested?.Invoke());
        ClearWorkCommand = new RelayCommand(ClearLinkedWork);
        OpenLinkedWorkCommand = new RelayCommand(() =>
        {
            if (_draftWorkItemId is not null)
            {
                OpenWorkRequested?.Invoke(_draftWorkItemId);
            }
        });
        session.DataChanged += () =>
        {
            ReloadWorkItems();
            ReloadRecent();
        };
        session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(AppSession.PinFloating))
            {
                Raise(nameof(PinLabel));
                Raise(nameof(AlwaysOnTopText));
            }

            if (args.PropertyName is nameof(AppSession.PinnedPreview)
                or nameof(AppSession.BoardCollapsed)
                or nameof(AppSession.HasBoardPin))
            {
                Raise(nameof(PinPreviewLabel));
                Raise(nameof(IsPreviewPinned));
                Raise(nameof(ShowPinnedItem));
                Raise(nameof(HasBoardContent));
            }
        };
        PendingFiles.CollectionChanged += OnPendingFilesChanged;
        RestoreDraft();
        ReloadWorkItems();
        ReloadRecent();
        RestoreBoardVisibility();
    }

    public AppSession Session { get; }

    public string Body
    {
        get => _body;
        set
        {
            if (_body == value)
            {
                return;
            }

            _body = value;
            Raise(nameof(Body));
            Raise(nameof(SingleLineSummary));
            Raise(nameof(ShowPlaceholder));
            OnComposerContentChanged();
        }
    }

    private string _workItemTitle = "";

    public string WorkItemTitle
    {
        get => _workItemTitle;
        set
        {
            if (_workItemTitle == value)
            {
                return;
            }

            _workItemTitle = value;
            Raise(nameof(WorkItemTitle));
            ((AsyncRelayCommand)AddWorkItemCommand).RaiseCanExecuteChanged();
        }
    }

    public string StatusText { get; private set; } = "Enter 기록 · Shift+Enter 긴 메모";

    public string ErrorText { get; private set; } = "";

    public string SaveLabel => _saveFailed ? "다시 기록" : "기록";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public bool HasPendingFiles => PendingFiles.Count > 0;

    public bool CanSave => !_isAttaching && (PendingFiles.Count > 0 || !string.IsNullOrWhiteSpace(Body));

    public bool CanAddWorkItem => !string.IsNullOrWhiteSpace(WorkItemTitle);

    public bool IsIdle => !_isAttaching;

    public bool ShowUndoComplete => _undoWorkItemId is not null;

    public bool CanUndoComplete => _undoWorkItemId is not null;

    public bool ShowRecordButton => CanSave && string.IsNullOrEmpty(_successFlash);

    public bool ShowSuccessFlash => !string.IsNullOrEmpty(_successFlash);

    public bool ShowPlaceholder => !ShowSuccessFlash && string.IsNullOrWhiteSpace(_body);

    public string SuccessFlash => _successFlash ?? "";

    public string Placeholder => "지금 떠오른 것을 기록하세요…";

    public CapsulePanelKind PanelKind => _panelKind;

    public CapsuleInputMode InputMode => _inputMode;

    public bool IsSingleLine => _inputMode == CapsuleInputMode.SingleLine;

    public bool IsMultiline => _inputMode == CapsuleInputMode.Multiline;

    public bool ShowRecentPanel => _panelKind == CapsulePanelKind.Recent;

    public bool ShowTodoPanel => _panelKind == CapsulePanelKind.Todo;

    public bool ShowDraftPanel => _panelKind == CapsulePanelKind.Draft;

    public bool ShowAuxiliaryPanel => ShowRecentPanel || ShowTodoPanel || ShowDraftPanel;

    public bool ShowAddTodoInput
    {
        get => _showAddTodoInput;
        private set => _showAddTodoInput = value;
    }

    public bool IsDraggingFiles
    {
        get => _isDraggingFiles;
        set
        {
            if (_isDraggingFiles == value)
            {
                return;
            }

            _isDraggingFiles = value;
            Raise(nameof(IsDraggingFiles));
        }
    }

    public bool IsPreviewPinned =>
        (ShowTodoPanel && Session.PinnedPreview == FloatingPinnedPreview.Todo)
        || (ShowRecentPanel && Session.PinnedPreview == FloatingPinnedPreview.Recent);

    public string PinPreviewLabel => IsPreviewPinned ? "패널 유지 켜짐" : "패널 유지";

    public bool ShowTodoBadge => OpenWorkItems.Count > 0;

    public string TodoBadgeText => OpenWorkItems.Count.ToString();

    public bool ShowAttachBadge => PendingFiles.Count > 0;

    public string AttachBadgeText => PendingFiles.Count.ToString();

    public bool HasMoreTodos => OpenWorkItems.Count > CapsuleLayout.PreviewRowCount;

    public string MoreTodosText => $"전체 {OpenWorkItems.Count}개 보기";

    public bool ShowTodoEmpty => OpenWorkItems.Count == 0;

    public bool ShowRecentEmpty => RecentRows.Count == 0 && !ShowPeek && !ShowPinnedItem;

    public bool HasBoardContent => RecentRows.Count > 0 || ShowPinnedItem;

    public bool ShowPinnedItem => Session.HasBoardPin && !string.IsNullOrWhiteSpace(_pinnedTitle);

    public string PinnedTitle => _pinnedTitle;

    public string PinnedKindLabel => _pinnedKindLabel;

    public bool ShowPeek => _peekRow is not null;

    public string PeekTitle => _peekRow?.TimeLabel ?? "";

    public string PeekBody => _peekRow?.Body ?? "";

    public string PeekFileSummary => _peekRow?.FileName ?? "";

    public bool ShowPeekFile => !string.IsNullOrWhiteSpace(_peekRow?.FileName);

    public bool ShowPeekImage => !string.IsNullOrWhiteSpace(_peekRow?.ImagePath);

    public string PeekImagePath => _peekRow?.ImagePath ?? "";

    public ImageSource? PeekImage => CapsuleImageLoader.TryLoad(_peekRow?.ImagePath, 160);

    public bool IsPeekPinned =>
        _peekRow is not null
        && Session.PinnedBoardKind == "note"
        && Session.PinnedBoardId == _peekRow.Id;

    public string PinPeekLabel => IsPeekPinned ? "고정 해제" : "고정한 메모";

    public string TodoFooterLabel => $"할 일 {OpenWorkItems.Count}개";

    public bool HasMorePending => PendingFiles.Count > CapsuleLayout.PreviewRowCount;

    public string MorePendingText => $"+{PendingFiles.Count - CapsuleLayout.PreviewRowCount}";

    public string DraftHint => HasPendingFiles
        ? $"첨부 {PendingFiles.Count} · 초안 보관됨"
        : string.IsNullOrWhiteSpace(Body) ? "초안 없음" : "긴 메모 초안";

    public string SingleLineSummary
    {
        get
        {
            if (!string.IsNullOrEmpty(_successFlash))
            {
                return _successFlash;
            }

            var text = (_body ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\n', ' ').Trim();
            return text;
        }
    }

    public bool CapsuleInputReadOnly => ShowDraftPanel;

    public ObservableCollection<PendingAttachment> PendingFiles { get; } = [];

    public ObservableCollection<PendingAttachment> VisiblePendingFiles { get; } = [];

    public ObservableCollection<OpenWorkItemRow> OpenWorkItems { get; } = [];

    public ObservableCollection<OpenWorkItemRow> VisibleWorkItems { get; } = [];

    public ObservableCollection<CapsuleRecentRow> RecentRows { get; } = [];

    public ICommand SaveNoteCommand { get; }
    public ICommand AddWorkItemCommand { get; }
    public ICommand TogglePinCommand { get; }
    public ICommand UndoCompleteCommand { get; }
    public ICommand OpenTodayCommand { get; }
    public ICommand ToggleRecentCommand { get; }
    public ICommand ToggleTodoCommand { get; }
    public ICommand ShowRecentCommand { get; }
    public ICommand OpenLongNoteCommand { get; }
    public ICommand HideCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand CaptureRegionCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand ToggleAddTodoCommand { get; }
    public ICommand TogglePinPreviewCommand { get; }
    public ICommand CollapseBoardCommand { get; }
    public ICommand ClosePeekCommand { get; }
    public ICommand PinPeekCommand { get; }
    public ICommand UnpinBoardCommand { get; }
    public ICommand CopyPeekCommand { get; }
    public ICommand OpenPeekInDayCommand { get; }
    public ICommand ShowStorageCommand { get; }
    public ICommand PickWorkCommand { get; }
    public ICommand ClearWorkCommand { get; }
    public ICommand OpenLinkedWorkCommand { get; }

    public bool ShowWorkChip => !string.IsNullOrEmpty(_draftWorkItemId);

    public string WorkChipText => FlowNote.Core.Rules.WorkChipText.ForCapsule(_draftWorkIssueKey, _draftWorkTitle ?? "");

    public string WorkChipToolTip
    {
        get
        {
            var name = _draftWorkTitle ?? "";
            if (_draftWorkStatus == WorkItemStatus.Completed)
            {
                return name + " · 완료된 업무. 초안은 유지됩니다.";
            }

            if (_draftWorkStatus == WorkItemStatus.Cancelled)
            {
                return name + " · 취소된 업무. 초안은 유지됩니다.";
            }

            return string.IsNullOrEmpty(_draftWorkIssueKey) ? name : _draftWorkIssueKey + " · " + name;
        }
    }

    public bool LinkedWorkClosed => _draftWorkStatus is WorkItemStatus.Completed or WorkItemStatus.Cancelled;

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action? OpenTodayRequested;

    public event Action? HideRequested;

    public event Action? ExitRequested;

    public event Action? RegionCaptureRequested;

    public event Action? AttachRequested;

    public event Action? StorageRequested;

    public event Action? PanelChanged;

    public event Action? SuccessFlashRequested;

    public event Action<CapsuleRecentRow>? OpenEntryRequested;

    public event Action<string>? OpenWorkRequested;

    public event Action? PickWorkRequested;

    public event Action? FocusInputRequested;

    public void BeginAttach()
    {
        _isAttaching = true;
        RaiseIdle();
    }

    public void EndAttach()
    {
        _isAttaching = false;
        RaiseIdle();
    }

    public void AddPending(PendingAttachment attachment)
    {
        try
        {
            var staged = Session.Database.Attachments.StageIncoming(attachment, PendingFiles.Count);
            PendingFiles.Add(staged);
            ErrorText = "";
            Raise(nameof(ErrorText));
            Raise(nameof(HasError));
            SaveDraft();
            SetPanel(CapsulePanelKind.Draft);
        }
        catch (ValidationException ex)
        {
            ErrorText = ex.Message;
            Raise(nameof(ErrorText));
            Raise(nameof(HasError));
            SetPanel(CapsulePanelKind.Draft);
        }
    }

    public void RemovePending(PendingAttachment attachment)
    {
        PendingFiles.Remove(attachment);
        SaveDraft();
    }

    public void OpenRecentEntry(CapsuleRecentRow row)
    {
        _peekRow = row;
        SetPanel(CapsulePanelKind.Recent);
        RaisePeek();
    }

    public void OpenWorkFromTodo(OpenWorkItemRow row)
    {
        if (Session.PinnedBoardKind == "todo" && Session.PinnedBoardId == row.Id)
        {
            Session.ClearBoardPin();
        }
        else
        {
            Session.SetBoardPin("todo", row.Id);
        }

        RefreshPinned();
        Raise(nameof(ShowPinnedItem));
        Raise(nameof(HasBoardContent));
    }

    public void TogglePanel(CapsulePanelKind kind)
    {
        SetPanel(_panelKind == kind ? CapsulePanelKind.None : kind);
    }

    public void SetPanel(CapsulePanelKind kind)
    {
        if (kind != CapsulePanelKind.Draft && _inputMode == CapsuleInputMode.Multiline && !BodyContainsNewline())
        {
            _inputMode = CapsuleInputMode.SingleLine;
        }

        if (kind == CapsulePanelKind.None && !BodyContainsNewline())
        {
            _inputMode = CapsuleInputMode.SingleLine;
        }

        _panelKind = kind;
        RaisePanel();
        PanelChanged?.Invoke();
    }

    public void CloseTransientPanel()
    {
        if (_panelKind is CapsulePanelKind.None)
        {
            return;
        }

        if (_panelKind == CapsulePanelKind.Todo && Session.PinnedPreview == FloatingPinnedPreview.Todo)
        {
            return;
        }

        if (_panelKind == CapsulePanelKind.Recent && Session.PinnedPreview == FloatingPinnedPreview.Recent)
        {
            return;
        }

        SetPanel(CapsulePanelKind.None);
    }

    public void OpenLongNote()
    {
        _inputMode = CapsuleInputMode.Multiline;
        Raise(nameof(InputMode));
        Raise(nameof(IsSingleLine));
        Raise(nameof(IsMultiline));
        Raise(nameof(CapsuleInputReadOnly));
        SetPanel(CapsulePanelKind.Draft);
        FocusInputRequested?.Invoke();
    }

    public void NotifyPossibleMultilinePaste()
    {
        if (BodyContainsNewline())
        {
            OpenLongNote();
        }
    }

    public void SetError(string message)
    {
        ErrorText = message;
        Raise(nameof(ErrorText));
        Raise(nameof(HasError));
        SetPanel(CapsulePanelKind.Draft);
    }

    public void ClearSuccessFlash()
    {
        if (string.IsNullOrEmpty(_successFlash))
        {
            return;
        }

        _successFlash = null;
        Raise(nameof(SuccessFlash));
        Raise(nameof(ShowSuccessFlash));
        Raise(nameof(ShowPlaceholder));
        Raise(nameof(ShowRecordButton));
        Raise(nameof(SingleLineSummary));
    }

    public void SaveDraft()
    {
        if (string.IsNullOrWhiteSpace(Body) && PendingFiles.Count == 0)
        {
            Session.Database.Drafts.Clear(DraftId);
            if (!_saveSucceeded && !_saveFailed)
            {
                StatusText = "Enter 기록 · Shift+Enter 긴 메모";
                Raise(nameof(StatusText));
            }

            NotifyCanSave();
            return;
        }

        Session.Database.Drafts.Save(
            DraftId,
            "note",
            Body ?? "",
            _draftWorkItemId,
            null,
            DraftAttachmentCodec.Serialize(PendingFiles));
        _saveSucceeded = false;
        StatusText = "초안 보관됨";
        Raise(nameof(StatusText));
        Raise(nameof(DraftHint));
        NotifyCanSave();
    }

    public async Task SaveNoteAsync()
    {
        var keepBody = Body;
        var keepFiles = PendingFiles.ToList();
        if (string.IsNullOrWhiteSpace(keepBody) && keepFiles.Count == 0)
        {
            return;
        }

        ErrorText = "";
        Raise(nameof(ErrorText));
        Raise(nameof(HasError));
        try
        {
            StatusText = "기록 중";
            Raise(nameof(StatusText));
            _submitRequestId ??= Guid.NewGuid().ToString("D");
            var submittedBody = keepBody ?? "";
            var submittedPaths = keepFiles.Select(static item => item.SourcePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var saved = await Session.Database.Entries.SaveNoteAsync(
                new SaveNoteRequest
                {
                    RequestId = _submitRequestId,
                    Body = submittedBody,
                    WorkItemId = _draftWorkItemId,
                    HasAttachments = keepFiles.Count > 0
                },
                keepFiles);
            foreach (var pending in keepFiles)
            {
                TryDeleteStaging(pending.SourcePath);
            }

            if (string.Equals(Body, submittedBody, StringComparison.Ordinal))
            {
                _body = "";
            }

            foreach (var pending in PendingFiles.Where(item => submittedPaths.Contains(item.SourcePath)).ToList())
            {
                PendingFiles.Remove(pending);
            }

            RefreshVisiblePending();
            if (string.IsNullOrWhiteSpace(Body) && PendingFiles.Count == 0)
            {
                Session.Database.Drafts.Clear(DraftId);
            }

            _submitRequestId = null;
            _saveFailed = false;
            _saveSucceeded = true;
            _inputMode = CapsuleInputMode.SingleLine;
            var local = TimeZoneInfo.ConvertTime(saved.RecordedAtUtc, Session.Database.DisplayTimeZone.TimeZone);
            _successFlash = $"{local:HH:mm} 기록됨";
            StatusText = _successFlash;
            try
            {
                Session.NotifyDataChanged();
            }
            catch (Exception)
            {
                StatusText = _successFlash + " · 목록 새로고침은 다음에";
            }

            ClosePeek();
            if (!Session.BoardCollapsed)
            {
                SetPanel(CapsulePanelKind.Recent);
            }

            SuccessFlashRequested?.Invoke();
        }
        catch (ValidationException ex)
        {
            Body = keepBody;
            RestorePending(keepFiles);
            _saveFailed = true;
            _saveSucceeded = false;
            StatusText = "기록하지 못했어요";
            ErrorText = ex.Message;
            SetPanel(CapsulePanelKind.Draft);
        }
        catch (DatabaseUnavailableException)
        {
            Body = keepBody;
            RestorePending(keepFiles);
            _saveFailed = true;
            _saveSucceeded = false;
            StatusText = "기록하지 못했어요";
            ErrorText = "저장하지 못했습니다. 입력은 그대로 둡니다.";
            SetPanel(CapsulePanelKind.Draft);
        }

        Raise(nameof(StatusText));
        Raise(nameof(ErrorText));
        Raise(nameof(HasError));
        Raise(nameof(Body));
        Raise(nameof(SingleLineSummary));
        Raise(nameof(SaveLabel));
        Raise(nameof(SuccessFlash));
        Raise(nameof(ShowSuccessFlash));
        Raise(nameof(ShowPlaceholder));
        Raise(nameof(InputMode));
        Raise(nameof(IsSingleLine));
        Raise(nameof(IsMultiline));
        Raise(nameof(CapsuleInputReadOnly));
        NotifyCanSave();
    }

    public async Task AddWorkItemAsync()
    {
        ErrorText = "";
        try
        {
            var created = await Session.Database.WorkItems.CreateAsync(new CreateWorkItemRequest
            {
                RequestId = Guid.NewGuid().ToString("D"),
                Title = WorkItemTitle
            });
            WorkItemTitle = "";
            Raise(nameof(WorkItemTitle));
            ShowAddTodoInput = false;
            Raise(nameof(ShowAddTodoInput));
            Session.NotifyDataChanged();
            if (!Session.BoardCollapsed)
            {
                SetPanel(CapsulePanelKind.Todo);
            }

            StatusText = "할 일을 남겼습니다";
            _saveSucceeded = false;
            _ = created;
        }
        catch (ValidationException ex)
        {
            ErrorText = ex.Message;
            Raise(nameof(HasError));
        }

        Raise(nameof(StatusText));
        Raise(nameof(ErrorText));
        ((AsyncRelayCommand)AddWorkItemCommand).RaiseCanExecuteChanged();
    }

    public async Task CompleteAsync(OpenWorkItemRow row)
    {
        if (row.IsBusy)
        {
            return;
        }

        row.IsBusy = true;
        row.IsChecked = true;
        try
        {
            var result = await Session.Database.WorkItems.CompleteAsync(new WorkItemCommandRequest
            {
                WorkItemId = row.Id,
                RequestId = Guid.NewGuid().ToString("D"),
                ExpectedVersion = row.Version
            });
            _undoWorkItemId = result.WorkItem.Id;
            _undoVersion = result.WorkItem.Version;
            _undoTitle = result.WorkItem.Title;
            StatusText = "완료 기록이 남았습니다";
            ErrorText = "";
            if (Session.PinnedBoardKind == "todo" && Session.PinnedBoardId == row.Id)
            {
                Session.ClearBoardPin();
            }

            Session.NotifyDataChanged();
            if (!Session.BoardCollapsed)
            {
                SetPanel(CapsulePanelKind.Recent);
            }
            Raise(nameof(ShowUndoComplete));
            ((AsyncRelayCommand)UndoCompleteCommand).RaiseCanExecuteChanged();
        }
        catch (Exception ex) when (ex is ValidationException or VersionConflictException or DatabaseUnavailableException)
        {
            row.IsChecked = false;
            row.IsBusy = false;
            ErrorText = ex is DatabaseUnavailableException
                ? "완료 기록을 남기지 못했습니다. 항목은 그대로 둡니다."
                : ex.Message;
            Session.NotifyDataChanged();
        }

        Raise(nameof(StatusText));
        Raise(nameof(ErrorText));
        Raise(nameof(HasError));
    }

    public async Task UndoCompleteAsync()
    {
        if (_undoWorkItemId is null)
        {
            return;
        }

        try
        {
            await Session.Database.WorkItems.ReopenAsync(new WorkItemCommandRequest
            {
                WorkItemId = _undoWorkItemId,
                RequestId = Guid.NewGuid().ToString("D"),
                ExpectedVersion = _undoVersion
            });
            _undoWorkItemId = null;
            StatusText = "다시 열림 기록이 남았습니다";
            ErrorText = "";
            Session.NotifyDataChanged();
        }
        catch (Exception ex) when (ex is ValidationException or VersionConflictException or DatabaseUnavailableException)
        {
            ErrorText = ex is DatabaseUnavailableException
                ? "실행 취소에 실패했습니다. 완료 기록은 그대로 둡니다."
                : ex.Message;
        }

        Raise(nameof(ShowUndoComplete));
        Raise(nameof(StatusText));
        Raise(nameof(ErrorText));
        Raise(nameof(HasError));
        ((AsyncRelayCommand)UndoCompleteCommand).RaiseCanExecuteChanged();
    }

    public void NotifyWorkItemTitleChanged()
        => ((AsyncRelayCommand)AddWorkItemCommand).RaiseCanExecuteChanged();

    private void TogglePinPreview()
    {
        if (ShowTodoPanel)
        {
            Session.SetPinnedPreview(Session.PinnedPreview == FloatingPinnedPreview.Todo
                ? FloatingPinnedPreview.None
                : FloatingPinnedPreview.Todo);
        }
        else if (ShowRecentPanel)
        {
            Session.SetPinnedPreview(Session.PinnedPreview == FloatingPinnedPreview.Recent
                ? FloatingPinnedPreview.None
                : FloatingPinnedPreview.Recent);
        }

        Raise(nameof(IsPreviewPinned));
        Raise(nameof(PinPreviewLabel));
    }

    private void RequestToday()
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Session.Database.DisplayTimeZone.TimeZone).DateTime);
        Session.SelectDate(today);
        OpenTodayRequested?.Invoke();
    }

    private void RestoreDraft()
    {
        var draft = Session.Database.Drafts.Load(DraftId);
        if (draft is null)
        {
            return;
        }

        _body = draft.Body;
        if (!string.IsNullOrEmpty(draft.WorkItemId))
        {
            ApplyLinkedWork(draft.WorkItemId, raise: false);
        }

        if (BodyContainsNewline())
        {
            _inputMode = CapsuleInputMode.Multiline;
        }

        var missing = 0;
        foreach (var pending in DraftAttachmentCodec.Deserialize(draft.StagingAttachmentsJson))
        {
            if (File.Exists(pending.SourcePath))
            {
                PendingFiles.Add(pending);
            }
            else
            {
                missing++;
            }
        }

        RefreshVisiblePending();
        StatusText = "초안 보관됨";
        if (missing > 0)
        {
            ErrorText = "초안 첨부 일부를 찾지 못했습니다. 본문은 그대로 둡니다.";
        }

        Raise(nameof(Body));
        Raise(nameof(SingleLineSummary));
        Raise(nameof(ErrorText));
        Raise(nameof(HasError));
        Raise(nameof(InputMode));
        Raise(nameof(IsSingleLine));
        Raise(nameof(IsMultiline));
        Raise(nameof(DraftHint));
        RaiseWorkChip();
        NotifyCanSave();
    }

    private void ReloadWorkItems()
    {
        OpenWorkItems.Clear();
        VisibleWorkItems.Clear();
        var open = Session.Database.WorkItems.ListByStatusAsync(WorkItemStatus.Open).GetAwaiter().GetResult();
        foreach (var item in open)
        {
            OpenWorkItems.Add(new OpenWorkItemRow(item.Id, item.Title, item.Version, item.IssueKey, item.NextActionText));
        }

        foreach (var item in OpenWorkItems.Take(CapsuleLayout.PreviewRowCount))
        {
            VisibleWorkItems.Add(item);
        }

        Raise(nameof(OpenCountText));
        Raise(nameof(ShowTodoBadge));
        Raise(nameof(TodoBadgeText));
        Raise(nameof(HasMoreTodos));
        Raise(nameof(MoreTodosText));
        Raise(nameof(ShowTodoEmpty));
        Raise(nameof(TodoFooterLabel));
        RefreshPinned();
    }

    private void ReloadRecent()
    {
        RecentRows.Clear();
        var entries = Session.Database.Entries.ListRecentVisible(CapsuleLayout.PreviewRowCount);
        foreach (var entry in entries)
        {
            RecentRows.Add(CapsuleRecentRow.From(entry, Session));
        }

        RefreshPinned();
        if (_peekRow is not null)
        {
            var latest = RecentRows.FirstOrDefault(row => row.Id == _peekRow.Id);
            if (latest is null)
            {
                var still = Session.Database.Entries.GetByIdAsync(_peekRow.Id).GetAwaiter().GetResult();
                if (still is not null && still.DeletedAtUtc is null)
                    latest = CapsuleRecentRow.From(still, Session);
            }

            if (latest is null)
                ClosePeek();
            else
            {
                _peekRow = latest;
                RaisePeek();
            }
        }

        Raise(nameof(ShowRecentEmpty));
        Raise(nameof(HasBoardContent));
        Raise(nameof(TodoFooterLabel));
    }

    private void RestorePending(IReadOnlyList<PendingAttachment> files)
    {
        if (PendingFiles.Count > 0)
        {
            return;
        }

        foreach (var file in files)
        {
            PendingFiles.Add(file);
        }

        RefreshVisiblePending();
    }

    private void OnPendingFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshVisiblePending();
        Raise(nameof(HasPendingFiles));
        Raise(nameof(ShowAttachBadge));
        Raise(nameof(AttachBadgeText));
        Raise(nameof(HasMorePending));
        Raise(nameof(MorePendingText));
        Raise(nameof(DraftHint));
        OnComposerContentChanged();
    }

    private void RefreshVisiblePending()
    {
        VisiblePendingFiles.Clear();
        foreach (var file in PendingFiles.Take(CapsuleLayout.PreviewRowCount))
        {
            VisiblePendingFiles.Add(file);
        }
    }

    private void OnComposerContentChanged()
    {
        ClearSuccessFlash();
        if (string.IsNullOrWhiteSpace(Body) && PendingFiles.Count == 0 && _saveFailed)
        {
            _saveFailed = false;
            ErrorText = "";
            Raise(nameof(ErrorText));
            Raise(nameof(HasError));
            Raise(nameof(SaveLabel));
        }

        if (_saveSucceeded && (!string.IsNullOrWhiteSpace(Body) || PendingFiles.Count > 0))
        {
            _saveSucceeded = false;
            StatusText = "초안 보관됨";
            Raise(nameof(StatusText));
        }

        Raise(nameof(DraftHint));
        NotifyCanSave();
    }

    private void NotifyCanSave()
    {
        Raise(nameof(CanSave));
        Raise(nameof(ShowRecordButton));
        ((AsyncRelayCommand)SaveNoteCommand).RaiseCanExecuteChanged();
    }

    private void RaiseIdle()
    {
        Raise(nameof(IsIdle));
        NotifyCanSave();
    }

    private void RaisePanel()
    {
        Raise(nameof(PanelKind));
        Raise(nameof(ShowRecentPanel));
        Raise(nameof(ShowTodoPanel));
        Raise(nameof(ShowDraftPanel));
        Raise(nameof(ShowAuxiliaryPanel));
        Raise(nameof(IsPreviewPinned));
        Raise(nameof(PinPreviewLabel));
        Raise(nameof(CapsuleInputReadOnly));
        Raise(nameof(InputMode));
        Raise(nameof(IsSingleLine));
        Raise(nameof(IsMultiline));
    }

    private bool BodyContainsNewline()
        => _body.Contains('\n', StringComparison.Ordinal) || _body.Contains('\r', StringComparison.Ordinal);

    private void TryDeleteStaging(string path)
    {
        try
        {
            var staging = Session.Database.Paths.StagingDirectory;
            if (Path.GetFullPath(path).StartsWith(Path.GetFullPath(staging) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    public string OpenCountText => $"남은 할 일 {OpenWorkItems.Count}";

    public string PinLabel => Session.PinFloating ? "고정됨" : "고정";

    public string AlwaysOnTopText => Session.PinFloating ? "항상 위 켜짐" : "항상 위";

    public string UndoTitle => _undoTitle;

    public bool HasOwnedDraft => DraftWorkLinkPolicy.HasOwnedDraft(Body, PendingFiles.Count);

    public DraftWorkLinkDecision PrepareContinue(string workItemId)
        => DraftWorkLinkPolicy.Decide(_draftWorkItemId, workItemId, HasOwnedDraft);

    public void ContinueRecording(string workItemId, bool relinkExistingDraft)
    {
        if (!relinkExistingDraft && HasOwnedDraft && !string.Equals(_draftWorkItemId, workItemId, StringComparison.Ordinal))
        {
            return;
        }

        ApplyLinkedWork(workItemId, raise: true);
        SaveDraft();
        FocusInputRequested?.Invoke();
    }

    public void ApplyLinkedWork(string workItemId, bool raise)
    {
        var item = Session.Database.WorkItems.GetAsync(workItemId).GetAwaiter().GetResult();
        _draftWorkItemId = workItemId;
        _draftWorkTitle = item?.Title ?? "업무";
        _draftWorkIssueKey = item?.IssueKey;
        _draftWorkStatus = item?.Status;
        if (raise)
        {
            RaiseWorkChip();
        }
    }

    public void ClearLinkedWork()
    {
        _draftWorkItemId = null;
        _draftWorkTitle = null;
        _draftWorkIssueKey = null;
        _draftWorkStatus = null;
        SaveDraft();
        RaiseWorkChip();
    }

    private void ToggleBoard()
    {
        if (_panelKind is CapsulePanelKind.Recent or CapsulePanelKind.Todo)
        {
            CollapseBoard();
            return;
        }

        Session.SetBoardCollapsed(false);
        if (RecentRows.Count > 0 || ShowPinnedItem)
        {
            SetPanel(CapsulePanelKind.Recent);
            return;
        }

        if (OpenWorkItems.Count > 0)
        {
            SetPanel(CapsulePanelKind.Todo);
        }
    }

    private void ShowTodosOnBoard()
    {
        Session.SetBoardCollapsed(false);
        ClosePeek();
        SetPanel(CapsulePanelKind.Todo);
    }

    private void ShowRecentOnBoard()
    {
        Session.SetBoardCollapsed(false);
        ClosePeek();
        SetPanel(CapsulePanelKind.Recent);
    }

    private void CollapseBoard()
    {
        Session.SetBoardCollapsed(true);
        ClosePeek();
        SetPanel(CapsulePanelKind.None);
    }

    private void RestoreBoardVisibility()
    {
        if (Session.BoardCollapsed || !HasBoardContent)
        {
            return;
        }

        SetPanel(CapsulePanelKind.Recent);
    }

    private void ClosePeek()
    {
        if (_peekRow is null)
        {
            return;
        }

        _peekRow = null;
        RaisePeek();
    }

    private void PinPeek()
    {
        if (_peekRow is null)
        {
            return;
        }

        if (IsPeekPinned)
        {
            Session.ClearBoardPin();
        }
        else
        {
            Session.SetBoardPin("note", _peekRow.Id);
        }

        RefreshPinned();
        Raise(nameof(ShowPinnedItem));
        Raise(nameof(HasBoardContent));
        Raise(nameof(IsPeekPinned));
        Raise(nameof(PinPeekLabel));
    }

    private void UnpinBoard()
    {
        Session.ClearBoardPin();
        RefreshPinned();
        Raise(nameof(ShowPinnedItem));
        Raise(nameof(HasBoardContent));
        Raise(nameof(IsPeekPinned));
        Raise(nameof(PinPeekLabel));
    }

    private void CopyPeek()
    {
        if (string.IsNullOrWhiteSpace(PeekBody))
        {
            return;
        }

        System.Windows.Clipboard.SetText(PeekBody);
        StatusText = "원문을 복사했습니다";
        Raise(nameof(StatusText));
    }

    private void OpenPeekInDay()
    {
        if (_peekRow is null)
        {
            return;
        }

        OpenEntryRequested?.Invoke(_peekRow);
    }

    private void RaisePeek()
    {
        Raise(nameof(ShowPeek));
        Raise(nameof(PeekTitle));
        Raise(nameof(PeekBody));
        Raise(nameof(PeekFileSummary));
        Raise(nameof(ShowPeekFile));
        Raise(nameof(ShowPeekImage));
        Raise(nameof(PeekImagePath));
        Raise(nameof(PeekImage));
        Raise(nameof(IsPeekPinned));
        Raise(nameof(PinPeekLabel));
        Raise(nameof(ShowRecentEmpty));
    }

    private void RefreshPinned()
    {
        _pinnedTitle = "";
        _pinnedKindLabel = "";
        if (!Session.HasBoardPin)
        {
            Raise(nameof(PinnedTitle));
            Raise(nameof(PinnedKindLabel));
            Raise(nameof(ShowPinnedItem));
            return;
        }

        if (Session.PinnedBoardKind == "note")
        {
            var entry = Session.Database.Entries.GetByIdAsync(Session.PinnedBoardId).GetAwaiter().GetResult();
            if (entry is null || entry.DeletedAtUtc is not null)
            {
                Session.ClearBoardPin();
            }
            else
            {
                _pinnedKindLabel = "고정한 메모";
                _pinnedTitle = string.IsNullOrWhiteSpace(entry.TitleSnapshot)
                    ? CapsuleRecentRow.FirstLineOf(entry.Body)
                    : entry.TitleSnapshot;
            }
        }
        else if (Session.PinnedBoardKind == "todo")
        {
            var work = Session.Database.WorkItems.GetAsync(Session.PinnedBoardId).GetAwaiter().GetResult();
            if (work is null || work.Status != WorkItemStatus.Open)
            {
                Session.ClearBoardPin();
            }
            else
            {
                _pinnedKindLabel = "고정한 일";
                _pinnedTitle = work.Title;
            }
        }

        Raise(nameof(PinnedTitle));
        Raise(nameof(PinnedKindLabel));
        Raise(nameof(ShowPinnedItem));
        Raise(nameof(HasBoardContent));
    }

    private void RaiseWorkChip()
    {
        Raise(nameof(ShowWorkChip));
        Raise(nameof(WorkChipText));
        Raise(nameof(WorkChipToolTip));
        Raise(nameof(LinkedWorkClosed));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
