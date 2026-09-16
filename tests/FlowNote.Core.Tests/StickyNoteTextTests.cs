using FlowNote.Core.Rules;
namespace FlowNote.Core.Tests;
public sealed class StickyNoteTextTests
{
    [Theory]
    [InlineData("첫 줄\n두 번째 줄", "첫 줄", false, "첫 줄\n두 번째 줄")]
    [InlineData("짧은 한 줄", "짧은 한 줄", false, "짧은 한 줄")]
    [InlineData("", "paste-123.png", true, "")]
    [InlineData("", "이전 제목만 있는 기록", false, "이전 제목만 있는 기록")]
    public void Note_is_one_body_without_generated_heading(string body, string legacy, bool attached, string expected)
        => Assert.Equal(expected, NoteDisplayRules.StickyText(body, legacy, attached));
}
