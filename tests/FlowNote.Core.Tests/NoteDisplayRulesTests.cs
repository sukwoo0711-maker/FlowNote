using FlowNote.Core.Rules;

namespace FlowNote.Core.Tests;

public sealed class NoteDisplayRulesTests
{
    [Fact]
    public void Single_line_title_is_not_repeated_as_preview()
    {
        Assert.Equal("", NoteDisplayRules.DisplayPreview("플로우노트", "플로우노트"));
    }

    [Fact]
    public void First_line_used_as_title_is_stripped_from_multiline_preview()
    {
        var preview = NoteDisplayRules.DisplayPreview("보드 전원", "보드 전원\n시퀀스를 다시 확인");
        Assert.Equal("시퀀스를 다시 확인", preview);
    }

    [Fact]
    public void Memo_kind_chrome_is_hidden_for_notes()
    {
        Assert.False(NoteDisplayRules.ShowKindChrome("메모", isEvent: false, isGroup: false));
        Assert.True(NoteDisplayRules.ShowKindChrome("완료", isEvent: true, isGroup: false));
        Assert.False(NoteDisplayRules.ShowKindChrome("그룹", isEvent: false, isGroup: true));
    }

    [Fact]
    public void Image_only_note_uses_neutral_title()
    {
        Assert.Equal("이미지 기록", NoteDisplayRules.NoteCardTitle(null, "", hasImage: true, hasFiles: true));
        Assert.Equal("보드 전원", NoteDisplayRules.NoteCardTitle("보드 전원", "", hasImage: true, hasFiles: true));
    }
}
