using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using FlowNote.Core.Errors;
using FlowNote.Core.Models;
using FlowNote.Core.Rules;
using FlowNote.Desktop.Commands;
using Microsoft.Win32;

namespace FlowNote.Desktop.ViewModels;

public sealed class DayReportViewModel : INotifyPropertyChanged
{
    private readonly AppSession _session;
    private ReportSnapshot? _snapshot;
    private bool _stale;
    private string? _filterWorkId;

    public DayReportViewModel(AppSession session, DateOnly date, string? filterWorkId)
    {
        _session = session;
        Date = date;
        _filterWorkId = filterWorkId;
        ToggleCommand = new RelayCommandParam(Toggle);
        ToggleFileCommand = new RelayCommandParam(ToggleFile);
        ExtraEntryId = "";
        AddOutOfRangeCommand = new RelayCommand(AddOutOfRange);
        ClearFilterCommand = new RelayCommand(() =>
        {
            _filterWorkId = null;
            Reload();
        });
        RefreshPreviewCommand = new RelayCommand(BuildPreview);
        CopyCommand = new AsyncRelayCommand(CopyAsync, () => CanExport);
        SaveZipCommand = new AsyncRelayCommand(SaveZipAsync, () => CanExport);
        Reload();
    }

    public DateOnly Date { get; }

    public string Header => $"하루 정리 · {Date:yyyy년 M월 d일} · {_session.Database.DisplayTimeZone.TimeZone.Id}";

    public string FilterText => _filterWorkId is null ? "전체 업무" : "이 업무만 표시 · 필터 해제 가능";

    public bool HasFilter => _filterWorkId is not null;

    public string SelectionSummary { get; private set; } = "선택 0 · 파일 0";

    public string PreviewText { get; private set; } = "원문을 선택하면 미리보기가 만들어집니다.";

    public string StatusText { get; private set; } = "";

    public string ExtraEntryId { get; set; } = "";

    public bool CanExport => _snapshot is not null && !_stale && SelectedIds.Count > 0;

    public bool ShowStale => _stale;

    public ObservableCollection<ReportRow> Rows { get; } = [];

    public HashSet<string> SelectedIds { get; } = new(StringComparer.Ordinal);

    public HashSet<string> SelectedFiles { get; } = new(StringComparer.Ordinal);

    public ICommand ToggleCommand { get; }
    public ICommand ToggleFileCommand { get; }
    public ICommand AddOutOfRangeCommand { get; }
    public ICommand ClearFilterCommand { get; }
    public ICommand RefreshPreviewCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand SaveZipCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Reload()
    {
        var extras = Rows.Where(static item => item.Candidate.OutOfRange).Select(static item => item.Candidate.Id).ToList();
        var catalog = _session.Database.Reports.ForDate(Date, _filterWorkId, extras);
        Rows.Clear();
        foreach (var item in catalog)
        {
            Rows.Add(new ReportRow(item, SelectedIds.Contains(item.Id), file => SelectedFiles.Contains(file)));
        }

        Raise(nameof(FilterText));
        Raise(nameof(HasFilter));
        Raise(nameof(Header));
        UpdateSummary();
        _snapshot = null;
        PreviewText = "원문을 선택하면 미리보기가 만들어집니다.";
        Raise(nameof(PreviewText));
        Raise(nameof(CanExport));
    }

    private void Toggle(object? parameter)
    {
        if (parameter is not ReportRow row)
        {
            return;
        }

        if (!SelectedIds.Add(row.Candidate.Id))
        {
            SelectedIds.Remove(row.Candidate.Id);
            foreach (var file in row.Candidate.Files)
            {
                SelectedFiles.Remove(file.AttachmentId);
            }
        }

        row.Refresh(SelectedIds.Contains(row.Candidate.Id), SelectedFiles.Contains);
        MarkDirty();
    }

    private void ToggleFile(object? parameter)
    {
        if (parameter is not ReportFileRow file)
        {
            return;
        }

        if (!SelectedIds.Contains(file.EntryId))
        {
            return;
        }

        if (!SelectedFiles.Add(file.AttachmentId))
        {
            SelectedFiles.Remove(file.AttachmentId);
        }

        var parent = Rows.FirstOrDefault(item => item.Candidate.Id == file.EntryId);
        parent?.Refresh(SelectedIds.Contains(file.EntryId), SelectedFiles.Contains);
        MarkDirty();
    }

    private void AddOutOfRange()
    {
        if (string.IsNullOrWhiteSpace(ExtraEntryId))
        {
            StatusText = "추가할 기록 ID를 입력하세요.";
            Raise(nameof(StatusText));
            return;
        }

        var extras = Rows.Where(static item => item.Candidate.OutOfRange).Select(static item => item.Candidate.Id).Append(ExtraEntryId.Trim()).Distinct().ToList();
        var catalog = _session.Database.Reports.ForDate(Date, _filterWorkId, extras);
        Rows.Clear();
        foreach (var item in catalog)
        {
            Rows.Add(new ReportRow(item, SelectedIds.Contains(item.Id), file => SelectedFiles.Contains(file)));
        }

        MarkDirty();
    }

    private void BuildPreview()
    {
        try
        {
            var catalog = Rows.Select(static item => item.Candidate).ToList();
            _snapshot = ReportBuilder.Build(new ReportBuildRequest
            {
                ReportDate = Date,
                TimeZoneDisplay = _session.Database.DisplayTimeZone.TimeZone.Id,
                SelectedIds = SelectedIds.ToList(),
                SelectedAttachmentIds = SelectedFiles.ToList(),
                Catalog = catalog
            }, DateTimeOffset.UtcNow);
            _stale = false;
            PreviewText = _snapshot.Markdown;
            StatusText = "미리보기 준비됨. 복사와 저장은 이 스냅샷을 사용합니다.";
        }
        catch (ValidationException ex)
        {
            _snapshot = null;
            StatusText = ex.Message;
        }

        Raise(nameof(PreviewText));
        Raise(nameof(StatusText));
        Raise(nameof(ShowStale));
        Raise(nameof(CanExport));
        ((AsyncRelayCommand)CopyCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)SaveZipCommand).RaiseCanExecuteChanged();
    }

    private async Task CopyAsync()
    {
        if (!EnsureFresh())
        {
            return;
        }

        try
        {
            Clipboard.SetText(_snapshot!.Markdown);
            StatusText = "텍스트를 복사했습니다.";
        }
        catch (Exception)
        {
            StatusText = "복사하지 못했습니다. 선택은 그대로 둡니다.";
        }

        Raise(nameof(StatusText));
        await Task.CompletedTask;
    }

    private async Task SaveZipAsync()
    {
        if (!EnsureFresh())
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "ZIP (*.zip)|*.zip",
            FileName = $"flownote-{Date:yyyyMMdd}.zip"
        };
        if (dialog.ShowDialog() != true)
        {
            StatusText = "저장을 취소했습니다. 선택은 그대로 둡니다.";
            Raise(nameof(StatusText));
            return;
        }

        try
        {
            var paths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in _snapshot!.Files)
            {
                var stored = _session.Database.Entries.ListAttachments(file.EntryId).FirstOrDefault(item => item.Id == file.AttachmentId);
                if (stored is null)
                {
                    throw new ValidationException($"파일 '{file.OriginalName}'을 찾지 못했습니다.");
                }

                paths[file.AttachmentId] = _session.Database.Attachments.ResolveFullPath(stored);
            }

            _session.Database.ReportZip.Write(_snapshot, dialog.FileName, paths);
            StatusText = "패키지를 저장했습니다.";
        }
        catch (ValidationException ex)
        {
            StatusText = ex.Message;
        }
        catch (IOException)
        {
            StatusText = "패키지를 저장하지 못했습니다. 선택은 그대로 둡니다.";
        }

        Raise(nameof(StatusText));
        await Task.CompletedTask;
    }

    private bool EnsureFresh()
    {
        if (_snapshot is null)
        {
            StatusText = "먼저 미리보기를 만드세요.";
            Raise(nameof(StatusText));
            return false;
        }

        var catalog = Rows.Select(static item => item.Candidate).ToList();
        if (ReportBuilder.IsStale(_snapshot, catalog))
        {
            _stale = true;
            StatusText = "내용이 변경됐어요. 미리보기를 갱신하세요.";
            Raise(nameof(StatusText));
            Raise(nameof(ShowStale));
            Raise(nameof(CanExport));
            return false;
        }

        return true;
    }

    private void MarkDirty()
    {
        UpdateSummary();
        if (_snapshot is not null)
        {
            _stale = true;
            StatusText = "내용이 변경됐어요. 미리보기를 갱신하세요.";
        }

        Raise(nameof(StatusText));
        Raise(nameof(ShowStale));
        Raise(nameof(CanExport));
    }

    private void UpdateSummary()
    {
        SelectionSummary = $"선택 {SelectedIds.Count} · 파일 {SelectedFiles.Count}";
        Raise(nameof(SelectionSummary));
    }

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class ReportRow : INotifyPropertyChanged
{
    public ReportRow(ReportCandidate candidate, bool selected, Func<string, bool> fileSelected)
    {
        Candidate = candidate;
        IsSelected = selected;
        Files = candidate.Files.Select(item => new ReportFileRow(item, fileSelected(item.AttachmentId))).ToList();
        Label = BuildLabel(candidate);
    }

    public ReportCandidate Candidate { get; }

    public string Label { get; }

    public bool IsSelected { get; private set; }

    public IReadOnlyList<ReportFileRow> Files { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh(bool selected, Func<string, bool> fileSelected)
    {
        IsSelected = selected;
        foreach (var file in Files)
        {
            file.IsSelected = fileSelected(file.AttachmentId);
            file.Raise();
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
    }

    private static string BuildLabel(ReportCandidate candidate)
    {
        var date = candidate.OutOfRange ? candidate.OccurredLocalDate.ToString("yyyy-MM-dd") + " · " : "";
        var title = candidate.Title ?? candidate.WorkTitle ?? candidate.Kind.ToString();
        return date + candidate.OccurredAtUtc.ToLocalTime().ToString("HH:mm") + " · " + title;
    }
}

public sealed class ReportFileRow : INotifyPropertyChanged
{
    public ReportFileRow(ReportFileCandidate file, bool selected)
    {
        AttachmentId = file.AttachmentId;
        EntryId = file.EntryId;
        Name = file.OriginalName;
        IsSelected = selected;
    }

    public string AttachmentId { get; }
    public string EntryId { get; }
    public string Name { get; }
    public bool IsSelected { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Raise() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
}

public sealed class RelayCommandParam : ICommand
{
    private readonly Action<object?> _execute;

    public RelayCommandParam(Action<object?> execute) => _execute = execute;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute(parameter);

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
