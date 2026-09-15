using System.Globalization;

namespace FlowNote.Core.Time;

public static class UtcInstant
{
    public const string StorageFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static DateTimeOffset TruncateToMilliseconds(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            utc.Second,
            utc.Millisecond,
            TimeSpan.Zero);
    }

    public static string ToStorage(DateTimeOffset value)
    {
        return TruncateToMilliseconds(value).ToString(StorageFormat, CultureInfo.InvariantCulture);
    }

    public static DateTimeOffset Parse(string value)
    {
        return DateTimeOffset.ParseExact(
            value,
            StorageFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
