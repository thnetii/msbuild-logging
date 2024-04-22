namespace THNETII.MSBuild.Logging;

public class MSBuildProjectLoggingState(PlatformConfiguration platformConfiguration)
{
    //public Guid ProjectStateId { get; } = Guid.NewGuid();
    //public MSBuildProjectLoggingState? Parent { get; }
    public PlatformConfiguration PlatformConfiguration { get; } = platformConfiguration
        ?? throw new ArgumentNullException(nameof(platformConfiguration));

    public MSBuildProjectLoggingState(MSBuildProjectLoggingState parent)
        : this((parent ?? throw new ArgumentNullException(nameof(parent))).PlatformConfiguration)
    {
        //Parent = parent;
    }
}
