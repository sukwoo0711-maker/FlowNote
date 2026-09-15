namespace FlowNote.Core.Tests;

public sealed class ProductInvariantTests
{
    [Fact]
    public void WorkItem_is_the_domain_name()
    {
        Assert.Equal("WorkItem", ProductInvariants.WorkItemTypeName);
        Assert.Null(typeof(ProductInvariants).Assembly.GetType("FlowNote.Core.Task"));
    }

    [Fact]
    public void Completion_is_not_deletion()
    {
        Assert.False(ProductInvariants.CompletionDeletesWorkItem);
    }

    [Fact]
    public void Recorded_time_is_not_work_start_or_duration()
    {
        Assert.False(ProductInvariants.TreatsRecordedAtAsWorkStart);
        Assert.False(ProductInvariants.TreatsRecordedIntervalAsWorkDuration);
    }

    [Fact]
    public void Core_does_not_reference_wpf_or_sqlite()
    {
        var referenced = typeof(ProductInvariants).Assembly
            .GetReferencedAssemblies()
            .Select(static name => name.Name)
            .Where(static name => name is not null)
            .ToArray();

        Assert.DoesNotContain(
            referenced,
            static name => name!.Contains("PresentationFramework", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            referenced,
            static name => name!.Contains("PresentationCore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            referenced,
            static name => name!.Contains("Microsoft.Data.Sqlite", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            referenced,
            static name => name!.Contains("System.Data.SQLite", StringComparison.OrdinalIgnoreCase));
    }
}
