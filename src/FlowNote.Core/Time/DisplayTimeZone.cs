using FlowNote.Core.Errors;

namespace FlowNote.Core.Time;

public sealed class DisplayTimeZone
{
    public DisplayTimeZone(TimeZoneInfo timeZone)
    {
        TimeZone = timeZone;
    }

    public TimeZoneInfo TimeZone { get; }

    public static DisplayTimeZone Korea()
    {
        return new DisplayTimeZone(Resolve("Korea Standard Time", "Asia/Seoul"));
    }

    public DateOnly GetLocalDate(DateTimeOffset utc)
    {
        var local = TimeZoneInfo.ConvertTime(utc.ToUniversalTime(), TimeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public int GetUtcOffsetMinutes(DateTimeOffset utc)
    {
        return (int)TimeZone.GetUtcOffset(utc.UtcDateTime).TotalMinutes;
    }

    public UtcRange GetUtcRange(DateOnly localDate)
    {
        var start = LocalToUtc(localDate, TimeOnly.MinValue);
        var end = LocalToUtc(localDate.AddDays(1), TimeOnly.MinValue);
        return new UtcRange(start, end);
    }

    public DateTimeOffset LocalToUtc(DateOnly date, TimeOnly time, TimeSpan? offsetWhenAmbiguous = null)
    {
        var unspecified = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        if (TimeZone.IsInvalidTime(unspecified))
        {
            throw new InvalidLocalTimeException("없는 시각입니다. 임의로 보정하지 않습니다.");
        }

        if (TimeZone.IsAmbiguousTime(unspecified))
        {
            if (offsetWhenAmbiguous is null)
            {
                throw new AmbiguousLocalTimeException("같은 시각이 두 번 있습니다. 오프셋을 선택하세요.");
            }

            return new DateTimeOffset(unspecified, offsetWhenAmbiguous.Value).ToUniversalTime();
        }

        var offset = TimeZone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset).ToUniversalTime();
    }

    private static TimeZoneInfo Resolve(string windowsId, string ianaId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(windowsId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
    }
}

public readonly record struct UtcRange(DateTimeOffset InclusiveStartUtc, DateTimeOffset ExclusiveEndUtc);
