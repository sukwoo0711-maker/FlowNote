using System.ComponentModel;

namespace FlowNote.Desktop.ViewModels;

public sealed class OpenWorkItemRow : INotifyPropertyChanged
{
    private bool _isChecked;
    private bool _isBusy;

    public OpenWorkItemRow(string id, string title, int version, string? issueKey = null, string? nextAction = null)
    {
        Id = id;
        Title = title;
        Version = version;
        IssueKey = issueKey;
        NextAction = nextAction;
    }

    public string Id { get; }

    public string Title { get; }

    public string? IssueKey { get; }

    public string? NextAction { get; }

    public string NextActionDisplay => string.IsNullOrWhiteSpace(NextAction) ? "다음 행동 미등록" : NextAction!;

    public int Version { get; set; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            Raise(nameof(IsChecked));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value)
            {
                return;
            }

            _isBusy = value;
            Raise(nameof(IsBusy));
            Raise(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
