namespace FlowNote.Core.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
