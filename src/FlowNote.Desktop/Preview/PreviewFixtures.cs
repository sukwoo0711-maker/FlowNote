using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using FlowNote.Core.Models;
using FlowNote.Desktop.Commands;
using FlowNote.Desktop.ViewModels;

namespace FlowNote.Desktop.Preview;

internal static class PreviewAssets
{
    public static string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "FlowNoteUiPreview");

    public static string BoardPng { get; }

    public static string UartLog { get; }

    public static string BrokenPng { get; }

    static PreviewAssets()
    {
        Directory.CreateDirectory(DirectoryPath);
        BoardPng = Path.Combine(DirectoryPath, "board.png");
        UartLog = Path.Combine(DirectoryPath, "uart.log");
        BrokenPng = Path.Combine(DirectoryPath, "broken.png");
        WriteBoardPng(BoardPng);
        File.WriteAllText(UartLog, "UART preview fixture\n[00:00:01] ready\n");
        File.WriteAllBytes(BrokenPng, [0x00, 0x01, 0x02, 0x03, 0x04]);
    }

    private static void WriteBoardPng(string path)
    {
        using var bitmap = new System.Drawing.Bitmap(96, 64);
        using var graphics = System.Drawing.Graphics.FromImage(bitmap);
        graphics.Clear(System.Drawing.Color.FromArgb(255, 53, 104, 232));
        graphics.FillRectangle(System.Drawing.Brushes.White, 12, 10, 28, 44);
        graphics.FillRectangle(System.Drawing.Brushes.Gainsboro, 48, 18, 36, 28);
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }
}

internal static class PreviewFixtures
{
    public static IReadOnlyList<TimelineRow> EntryCards() =>
    [
        Note("empty-like", "09:12", "보드 전원 시퀀스를 확인했다", "보드 전원 시퀀스를 확인했다", selected: false),
        Note(
            "selected",
            "09:18",
            "로그 캡처",
            "UART 출력이 비었다\n재현 순서를 적어 둔다",
            selected: true,
            previews:
            [
                FilePreview("uart.log", PreviewAssets.UartLog, image: false)
            ]),
        Note(
            "image",
            "10:04",
            "보드 사진",
            "보드 사진",
            selected: false,
            previews:
            [
                FilePreview("board.png", PreviewAssets.BoardPng, image: true),
                FilePreview("broken.png", PreviewAssets.BrokenPng, image: true)
            ])
    ];

    public static IReadOnlyList<TimelineRow> EventRows() =>
    [
        Event("c1", "09:42", "할 일 생성", "created", "전원 시퀀스 재확인"),
        Event("c2", "11:03", "완료", "completed", "전원 시퀀스 재확인"),
        Event("c3", "11:40", "다시 열림", "reopened", "전원 시퀀스 재확인")
    ];

    public static PreviewComposerState EmptyComposer() => new()
    {
        StatusText = "저장하면 타임라인에 남습니다"
    };

    public static PreviewComposerState InputComposer() => new()
    {
        Body = "헤더 레지스터를 다시 읽는다",
        StatusText = "초안 보관됨"
    };

    public static PreviewComposerState AttachmentComposer() => new()
    {
        Body = "로그를 남긴다",
        StatusText = "초안 보관됨",
        PendingFiles =
        [
            new PendingAttachment
            {
                OriginalName = "uart.log",
                SourcePath = PreviewAssets.UartLog,
                MediaType = "text/plain",
                ByteSize = new FileInfo(PreviewAssets.UartLog).Length
            },
            new PendingAttachment
            {
                OriginalName = "board.png",
                SourcePath = PreviewAssets.BoardPng,
                MediaType = "image/png",
                ByteSize = new FileInfo(PreviewAssets.BoardPng).Length
            }
        ]
    };

    public static PreviewComposerState ErrorComposer() => new()
    {
        Body = "이 입력은 유지되어야 한다",
        StatusText = "기록하지 못했어요",
        ErrorText = "저장하지 못했습니다. 입력은 그대로 둡니다."
    };

    public static IReadOnlyList<OpenWorkItemRow> WorkItems() =>
    [
        new("w1", "전원 시퀀스 재확인", 1),
        new("w2", "보드 전원 시퀀스를 다시 확인하고 UART 로그를 정리한다", 1)
    ];

    private static AttachmentPreview FilePreview(string name, string path, bool image)
        => new()
        {
            Name = name,
            Path = path,
            IsImage = image,
            ByteSize = File.Exists(path) ? new FileInfo(path).Length : 0
        };

    private static TimelineRow Note(
        string id,
        string time,
        string title,
        string preview,
        bool selected,
        IReadOnlyList<AttachmentPreview>? previews = null)
        => new()
        {
            Id = id,
            TimeLabel = time,
            Title = title,
            Preview = preview,
            FullBody = preview,
            KindLabel = "메모",
            TimeDetail = "",
            AttachmentSummary = previews is { Count: > 0 }
                ? string.Join(", ", previews.Select(item => item.Name))
                : "첨부 없음",
            IsSelected = selected,
            NodeKind = "note",
            AttachmentPreviews = previews ?? []
        };

    private static TimelineRow Event(string id, string time, string kind, string nodeKind, string title)
        => new()
        {
            Id = id,
            TimeLabel = time,
            Title = title,
            Preview = "",
            FullBody = "",
            KindLabel = kind,
            TimeDetail = "",
            AttachmentSummary = "첨부 없음",
            IsEvent = true,
            NodeKind = nodeKind
        };
}

internal sealed class PreviewComposerState : IPendingAttachmentHost, INotifyPropertyChanged
{
    public string Body { get; init; } = "";

    public string StatusText { get; init; } = "";

    public string ErrorText { get; init; } = "";

    public ObservableCollection<PendingAttachment> PendingFiles { get; init; } = [];

    public bool IsIdle => true;

    public bool HasPendingFiles => PendingFiles.Count > 0;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public string SaveLabel => HasError ? "다시 기록" : "기록";

    public bool CanSave => PendingFiles.Count > 0 || !string.IsNullOrWhiteSpace(Body);

    public ICommand SaveNoteCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public PreviewComposerState()
    {
        SaveNoteCommand = new RelayCommand(static () => { }, () => CanSave);
    }

    public void RemovePending(PendingAttachment attachment)
    {
        PendingFiles.Remove(attachment);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPendingFiles)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
        ((RelayCommand)SaveNoteCommand).RaiseCanExecuteChanged();
    }
}
