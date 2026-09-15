using FlowNote.Core.Rules;

namespace FlowNote.Core.Tests;

public sealed class ImeEnterGuardTests
{
    [Fact]
    public void PlainEnter_saves()
    {
        var guard = new ImeEnterGuard();
        Assert.True(guard.ShouldSaveOnEnter(isImeProcessedKey: false));
    }

    [Fact]
    public void ComposingEnter_does_not_save()
    {
        var guard = new ImeEnterGuard();
        guard.OnCompositionStart();
        guard.OnCompositionUpdate();
        Assert.False(guard.ShouldSaveOnEnter(isImeProcessedKey: false));
        Assert.False(guard.ShouldSaveOnEnter(isImeProcessedKey: true));
    }

    [Fact]
    public void CompositionConfirmEnter_does_not_save()
    {
        var guard = new ImeEnterGuard();
        guard.OnCompositionStart();
        guard.OnCompositionEnd();
        Assert.False(guard.ShouldSaveOnEnter(isImeProcessedKey: false));
        Assert.True(guard.ShouldSaveOnEnter(isImeProcessedKey: false));
    }

    [Fact]
    public void ImeProcessed_does_not_save()
    {
        var guard = new ImeEnterGuard();
        Assert.False(guard.ShouldSaveOnEnter(isImeProcessedKey: true));
    }

    [Fact]
    public void ClearConfirmPending_allows_later_enter()
    {
        var guard = new ImeEnterGuard();
        guard.OnCompositionStart();
        guard.OnCompositionEnd();
        guard.ClearConfirmPending();
        Assert.True(guard.ShouldSaveOnEnter(isImeProcessedKey: false));
    }
}
