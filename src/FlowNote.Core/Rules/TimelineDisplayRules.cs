using FlowNote.Core.Models;

namespace FlowNote.Core.Rules;

public static class TimelineDisplayRules
{
    public static string NodeKind(EntryKind kind) => kind switch
    {
        EntryKind.Note => "note",
        EntryKind.TaskCreated => "created",
        EntryKind.TaskCompleted => "completed",
        EntryKind.TaskReopened => "reopened",
        EntryKind.TaskCancelled => "cancelled",
        _ => "note"
    };

    public static string KindLabel(EntryKind kind) => kind switch
    {
        EntryKind.Note => "메모",
        EntryKind.TaskCreated => "할 일 생성",
        EntryKind.TaskCompleted => "완료",
        EntryKind.TaskReopened => "다시 열림",
        EntryKind.TaskCancelled => "취소됨",
        _ => kind.ToString()
    };

    public static string? TimeDifferenceLabel(DateTimeOffset occurredLocal, DateTimeOffset recordedLocal)
    {
        if (occurredLocal == recordedLocal)
        {
            return null;
        }

        return $"발생 {occurredLocal:HH:mm} · 기록 {recordedLocal:HH:mm}";
    }
}
