namespace FlowNote.Core.Assist;

public static class InferenceValidator
{
    public static string? Validate(InferenceRequest request, InferenceResult result)
    {
        if (!string.Equals(request.EntryId, result.EntryId, StringComparison.Ordinal))
        {
            return "entry-id-mismatch";
        }

        if (result.Decision == AssistDecision.Abstain)
        {
            if (result.Primary is not null || result.Mentions.Count > 0)
            {
                return "abstain-not-empty";
            }

            return null;
        }

        if (result.Primary is null)
        {
            return "primary-required";
        }

        if (result.Mentions.Count > AssistVersions.MaxMentions)
        {
            return "too-many-mentions";
        }

        var note = request.NoteText;
        var primaryError = ValidatePrimary(request, result.Primary, result.Decision, allowPerformed: true);
        if (primaryError is not null)
        {
            return primaryError;
        }

        foreach (var mention in result.Mentions)
        {
            if (mention.Role is ContextRole.Performed or ContextRole.CompletionMention or ContextRole.Unknown)
            {
                return "mention-role-invalid";
            }

            if (result.Primary.ThreadId is not null &&
                string.Equals(mention.ThreadId, result.Primary.ThreadId, StringComparison.Ordinal))
            {
                return "mention-duplicates-primary";
            }

            var mentionError = ValidatePrimary(request, mention, AssistDecision.Link, allowPerformed: false);
            if (mentionError is not null && mention.ThreadId is null)
            {
                mentionError = ValidatePrimary(request, mention, AssistDecision.New, allowPerformed: false);
            }

            if (mentionError is not null)
            {
                return mentionError;
            }

            if (!AssistText.IsExactSubstring(note, mention.SourceQuote))
            {
                return "mention-quote-missing";
            }
        }

        return null;
    }

    private static string? ValidatePrimary(InferenceRequest request, InferencePrimary primary, AssistDecision decision, bool allowPerformed)
    {
        if (!AssistText.IsExactSubstring(request.NoteText, primary.SourceQuote) ||
            primary.SourceQuote.Length > AssistVersions.SourceQuoteMax)
        {
            return "source-quote-invalid";
        }

        if (primary.NextActionQuote is not null &&
            !AssistText.IsExactSubstring(request.NoteText, primary.NextActionQuote))
        {
            return "next-action-quote-invalid";
        }

        if (decision == AssistDecision.Link)
        {
            if (primary.TopicQuote is not null)
            {
                return "link-topic-must-be-null";
            }

            if (primary.ThreadId is null || request.Candidates.All(item => item.ThreadId != primary.ThreadId))
            {
                return "link-unknown-thread";
            }
        }

        if (decision == AssistDecision.New)
        {
            if (primary.ThreadId is not null)
            {
                return "new-thread-must-be-null";
            }

            if (primary.TopicQuote is null ||
                !AssistText.IsExactSubstring(request.NoteText, primary.TopicQuote) ||
                AssistText.IsGenericTopic(primary.TopicQuote))
            {
                return "new-topic-invalid";
            }
        }

        if (!allowPerformed && primary.Role == ContextRole.Performed)
        {
            return "mention-performed";
        }

        return null;
    }
}
