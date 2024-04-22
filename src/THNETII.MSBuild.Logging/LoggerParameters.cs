namespace THNETII.MSBuild.Logging;

public class LoggerParameters
{
    private const char nameValueDelimiter = '=';
#if !NETCOREAPP
    private static readonly string nameValueDelimiterString = nameValueDelimiter.ToString();
#endif
    private const char nameValuePairDelimiter = '|';

    private readonly IDictionary<string, string> parameters;

    public string? this[string name]
    {
        get
        {
            _ = parameters.TryGetValue(name, out string? value);
            return value;
        }
    }

    private LoggerParameters(IDictionary<string, string> parameters)
    {
        this.parameters = parameters;
    }

    public override string ToString() => string.Join(
#if NETCOREAPP
        nameValuePairDelimiter
#else
        nameValueDelimiterString
#endif
        ,
        parameters.Select(static p => $"{p.Key}{nameValueDelimiter}{p.Value}")
        );

    public static LoggerParameters Parse(string paramString)
    {
        if (string.IsNullOrEmpty(paramString))
        {
            return new LoggerParameters(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
        var parameters = paramString.Split(nameValuePairDelimiter)
            .Select(static (pair) => (
                text: pair,
                delimIdx: pair.IndexOf(
                    nameValueDelimiter
#if NETCOREAPP
                    , StringComparison.Ordinal
#endif
                    )
                )
            )
            .Where(static (p) => p.delimIdx >= 0)
            .ToDictionary(
                static (p) => p.text[..p.delimIdx].Trim(),
                static (p) => p.text[(p.delimIdx + 1)..].Trim(),
                StringComparer.OrdinalIgnoreCase
                );
        return new LoggerParameters(parameters);
    }
}
