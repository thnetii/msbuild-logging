using System.Collections;

using Microsoft.Build.Framework;

namespace THNETII.MSBuild.Logging;

public class PlatformConfiguration(string platform, string configuration)
{
    public static PlatformConfiguration Empty { get; } = new(string.Empty, string.Empty);

    private readonly HashSet<string> uniqueProjects = new(StringComparer.OrdinalIgnoreCase);

    public string Platform { get; } = platform ?? throw new ArgumentNullException(nameof(platform));
    public string Configuration { get; } = configuration ?? throw new ArgumentNullException(nameof(configuration));

    public bool AddProject(ProjectStartedEventArgs startedEvent) =>
        AddProject(startedEvent?.ProjectFile, startedEvent?.TargetNames);

    public bool AddProject(ExternalProjectStartedEventArgs startedEvent) =>
        AddProject(startedEvent?.ProjectFile, startedEvent?.TargetNames);

    private bool AddProject(string? projectFile, string? targetNames)
    {
        projectFile ??= string.Empty;
        targetNames ??= string.Empty;
        string projectToken = projectFile + "||" + targetNames;
        return uniqueProjects.Add(projectToken);
    }

    public static PlatformConfiguration Create(ProjectStartedEventArgs args)
    {
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(args);
#else
        _ = args ?? throw new ArgumentNullException(nameof(args));
#endif

        string? platform = default;
        string? configuration = default;

        foreach (var property in args.Properties?.Cast<DictionaryEntry>() ?? [])
        {
            if (string.Equals(nameof(Platform), property.Key as string, StringComparison.OrdinalIgnoreCase))
                platform = property.Value as string;
            else if (string.Equals(nameof(Configuration), property.Key as string, StringComparison.OrdinalIgnoreCase))
                configuration = property.Value as string;
        }

        return new(platform ?? string.Empty, configuration ?? string.Empty);
    }
}
