using FlowNote.Core.Errors;
using FlowNote.Core.Models;

namespace FlowNote.Core.Rules;

public static class NextActionRules
{
    public static string? ValidateAndNormalize(string? text, NextActionChangeReason reason)
    {
        if (reason is NextActionChangeReason.Clear or NextActionChangeReason.Completion)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("공백만 있는 다음 행동은 저장하지 않습니다. 지우려면 지우기를 누르세요.");
        }

        if (text.Length > AppLimits.MaxNextActionLength)
        {
            throw new ValidationException($"다음 행동은 {AppLimits.MaxNextActionLength}자를 넘길 수 없습니다. 잘라서 저장하지 않습니다.");
        }

        return text.Trim();
    }

    public static void EnsureCanActivate(WorkItemStatus status, NextActionChangeReason reason)
    {
        if (status == WorkItemStatus.Open)
        {
            return;
        }

        if (reason is NextActionChangeReason.Set or NextActionChangeReason.Update or NextActionChangeReason.Restore)
        {
            throw new ValidationException("완료되거나 취소된 업무에 다음 행동을 남기려면 먼저 다시 여세요.");
        }
    }

    public static NextActionAsOf ProjectAsOf(IReadOnlyList<NextActionChange> history, DateTimeOffset exclusiveEndUtc)
    {
        NextActionChange? last = null;
        foreach (var change in history.OrderBy(static item => item.ChangedAtUtc).ThenBy(static item => item.Id, StringComparer.Ordinal))
        {
            if (change.ChangedAtUtc >= exclusiveEndUtc)
            {
                continue;
            }

            last = change;
        }

        if (last is null)
        {
            return new NextActionAsOf(null, Recorded: false, null, null);
        }

        return new NextActionAsOf(last.NewText, Recorded: true, last.NewSourceEntryId, last.NewSourceRevision);
    }

    public static string? LastClearedText(IReadOnlyList<NextActionChange> history)
    {
        return history
            .Where(static item => item.Reason is NextActionChangeReason.Completion or NextActionChangeReason.Clear)
            .OrderBy(static item => item.ChangedAtUtc)
            .ThenBy(static item => item.Id, StringComparer.Ordinal)
            .LastOrDefault(static item => !string.IsNullOrEmpty(item.PreviousText))
            ?.PreviousText;
    }
}
