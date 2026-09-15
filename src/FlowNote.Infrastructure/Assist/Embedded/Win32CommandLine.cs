using System.Text;

namespace FlowNote.Infrastructure.Assist.Embedded;

public static class Win32CommandLine
{
    public static string Build(string executable, IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder();
        builder.Append(Quote(executable));
        foreach (var argument in arguments)
        {
            builder.Append(' ');
            builder.Append(Quote(argument));
        }

        return builder.ToString();
    }

    public static string Quote(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0)
        {
            return argument;
        }

        var builder = new StringBuilder();
        builder.Append('"');
        var i = 0;
        while (true)
        {
            var slashes = 0;
            while (i < argument.Length && argument[i] == '\\')
            {
                slashes++;
                i++;
            }

            if (i == argument.Length)
            {
                builder.Append('\\', slashes * 2);
                break;
            }

            if (argument[i] == '"')
            {
                builder.Append('\\', slashes * 2 + 1);
                builder.Append('"');
            }
            else
            {
                builder.Append('\\', slashes);
                builder.Append(argument[i]);
            }

            i++;
        }

        builder.Append('"');
        return builder.ToString();
    }
}
