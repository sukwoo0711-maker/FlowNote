namespace FlowNote.Core.Errors;

public sealed class ValidationException : Exception
{
    public ValidationException(string message)
        : base(message)
    {
    }
}

public sealed class DatabaseUnavailableException : Exception
{
    public DatabaseUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

public sealed class InvalidLocalTimeException : Exception
{
    public InvalidLocalTimeException(string message)
        : base(message)
    {
    }
}

public sealed class AmbiguousLocalTimeException : Exception
{
    public AmbiguousLocalTimeException(string message)
        : base(message)
    {
    }
}

public sealed class VersionConflictException : Exception
{
    public VersionConflictException(string message)
        : base(message)
    {
    }
}
