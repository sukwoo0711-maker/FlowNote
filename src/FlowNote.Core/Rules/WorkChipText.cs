namespace FlowNote.Core.Rules;

public static class WorkChipText
{
    public static string ForCapsule(string? issueKey, string title)
    {
        if (!string.IsNullOrWhiteSpace(issueKey))
        {
            return issueKey.Trim();
        }

        var trimmed = title.Trim();
        return trimmed.Length <= 16 ? trimmed : trimmed[..16];
    }

    public static string StatusLabel(Models.WorkItemStatus status) => status switch
    {
        Models.WorkItemStatus.Open => "현재 진행 중",
        Models.WorkItemStatus.Completed => "완료",
        Models.WorkItemStatus.Cancelled => "취소",
        _ => status.ToString()
    };
}
