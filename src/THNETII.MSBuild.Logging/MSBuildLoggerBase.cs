using Microsoft.Build.Framework;

namespace THNETII.MSBuild.Logging;

public abstract class MSBuildLoggerBase : ILogger
{
    private static readonly BuildEventContextComparer ctxCmp = BuildEventContextComparer.Instance;
    private static readonly char[] commaDelimiter = [','];

    private LoggerVerbosity verbosity;
    private readonly HashSet<PlatformConfiguration> configurations = [];
    private readonly Dictionary<BuildEventContext, MSBuildProjectLoggingState> projects = new(ctxCmp);
    private readonly Dictionary<BuildEventContext, List<BuildEventArgs>> orphans = new(ctxCmp);
    private HashSet<string> ignoredTargets = default!;

    public LoggerVerbosity Verbosity
    {
        get => verbosity;
        set => verbosity = value;
    }

    public string? Parameters { get; set; }

    public void Initialize(IEventSource eventSource)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(eventSource);
#else
        _ = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
#endif

        var loggerParameters = LoggerParameters.Parse(Parameters ?? string.Empty);
        if (!Enum.TryParse(loggerParameters[nameof(Verbosity)], ignoreCase: true, out verbosity))
            verbosity = LoggerVerbosity.Normal;
        ignoredTargets = new(
            ["GetCopyToOutputDirectoryItems", "GetNativeManifest", "GetTargetPath"],
            StringComparer.OrdinalIgnoreCase
            );
        string? ignoredTargetsParams = loggerParameters["TargetsNotLogged"];
        if (!string.IsNullOrEmpty(ignoredTargetsParams))
        {
            ignoredTargets.UnionWith(ignoredTargetsParams!.Split(commaDelimiter, StringSplitOptions.RemoveEmptyEntries));
        }

        string? solutionDirectory = loggerParameters["SolutionDir"];

        eventSource.ProjectStarted += HandleProjectStarted;
        eventSource.ProjectFinished += HandleProjectFinished;
        eventSource.CustomEventRaised += HandleCustomEventRaised;
        eventSource.ErrorRaised += HandleErrorRaised;
        eventSource.WarningRaised += HandleWarningRaised;
        eventSource.MessageRaised += HandleMessageRaised;
    }

    private void HandleProjectStarted(object sender, ProjectStartedEventArgs e)
    {
        if (ignoredTargets.Contains(e.TargetNames ?? string.Empty)) return;
        BuildEventContext? parentContext = e.ParentProjectBuildEventContext;
        BuildEventContext buildEventContext = e.BuildEventContext ??
            BuildEventContext.Invalid;
        if (ctxCmp.Equals(parentContext, default))
        {
            PlatformConfiguration platformConfiguration = PlatformConfiguration.Create(e);
            configurations.Add(platformConfiguration);
            MSBuildProjectLoggingState projectState = new(platformConfiguration);
            projects.Add(buildEventContext, projectState);
            // Trigger started
            UpdateOrphanedProjects(buildEventContext);
        }
        else if (projects.TryGetValue(parentContext, out var parentState))
        {
            if (!parentState.PlatformConfiguration.AddProject(e))
            {
                RemoveOrphanedProjects(buildEventContext);
                return;
            }
            MSBuildProjectLoggingState projectState = new(parentState);
            projects.Add(buildEventContext, projectState);
            // Trigger started
            UpdateOrphanedProjects(buildEventContext);
        }
        else
        {
            orphans.Add(buildEventContext, [e]);
        }
    }

    private void HandleProjectFinished(object sender, ProjectFinishedEventArgs e)
    {
        BuildEventContext buildEventContext = e.BuildEventContext
            ?? BuildEventContext.Invalid;
        if (projects.TryGetValue(buildEventContext, out var projectState))
        {

        }
        else if (orphans.TryGetValue(buildEventContext, out var orphanedEventList))
        {
            orphanedEventList.Add(e);
        }
    }

    private void HandleCustomEventRaised(object sender, CustomBuildEventArgs e)
    {
        switch (e)
        {
            case ExternalProjectStartedEventArgs extStart:
                HandleExternalProjectStarted(sender, extStart);
                break;
            case ExternalProjectFinishedEventArgs extFinish:
                HandleExternalProjectFinished(sender, extFinish);
                break;
        }
    }

    private void HandleExternalProjectStarted(object sender, ExternalProjectStartedEventArgs e)
    {
        BuildEventContext parentContext = e.BuildEventContext ?? BuildEventContext.Invalid;
        BuildEventContext extEventContext = GetExternalProjectEventContext(parentContext);
        if (projects.TryGetValue(parentContext, out var parentState))
        {
            if (!parentState.PlatformConfiguration.AddProject(e))
            {
                RemoveOrphanedProjects(extEventContext);
                return;
            }
            MSBuildProjectLoggingState projectState = new(parentState);
            projects.Add(extEventContext, projectState);
            // Trigger started
        }
        else
        {
            orphans.Add(extEventContext, [e]);
        }
    }

    private void HandleExternalProjectFinished(object sender, ExternalProjectFinishedEventArgs e)
    {
        BuildEventContext buildEventContext = GetExternalProjectEventContext(
            e.BuildEventContext ?? BuildEventContext.Invalid);
        if (projects.TryGetValue(buildEventContext, out var projectState))
        {

        }
        else if (orphans.TryGetValue(buildEventContext, out var orphanedEventList))
        {
            orphanedEventList.Add(e);
        }
    }

    private void HandleErrorRaised(object sender, BuildErrorEventArgs e)
    {
        BuildEventContext buildEventContext = e.BuildEventContext ??
            BuildEventContext.Invalid;
        BuildEventContext externalEventContext = GetExternalProjectEventContext(buildEventContext);
        if (TryHandleProjectEventArgs(externalEventContext, e, HandleProjectStateEvent) ||
            TryHandleProjectEventArgs(buildEventContext, e, HandleProjectStateEvent))
            return;

        // Synthetic project state

        static void HandleProjectStateEvent(BuildEventContext buildEventContext, MSBuildProjectLoggingState projectState, BuildErrorEventArgs e)
        {

        }
    }

    private void HandleWarningRaised(object sender, BuildWarningEventArgs e)
    {
        BuildEventContext buildEventContext = e.BuildEventContext ??
            BuildEventContext.Invalid;
        BuildEventContext externalEventContext = GetExternalProjectEventContext(buildEventContext);
        if (TryHandleProjectEventArgs(externalEventContext, e, HandleProjectStateEvent) ||
            TryHandleProjectEventArgs(buildEventContext, e, HandleProjectStateEvent))
            return;

        // Synthetic project state

        static void HandleProjectStateEvent(BuildEventContext buildEventContext, MSBuildProjectLoggingState projectState, BuildWarningEventArgs e)
        {

        }
    }

    private void HandleMessageRaised(object sender, BuildMessageEventArgs e)
    {
        BuildEventContext buildEventContext = e.BuildEventContext ??
            BuildEventContext.Invalid;
        BuildEventContext externalEventContext = GetExternalProjectEventContext(buildEventContext);
        if (TryHandleProjectEventArgs(externalEventContext, e, HandleProjectStateEvent) ||
            TryHandleProjectEventArgs(buildEventContext, e, HandleProjectStateEvent))
            return;

        // Synthetic project state

        static void HandleProjectStateEvent(BuildEventContext buildEventContext, MSBuildProjectLoggingState projectState, BuildMessageEventArgs e)
        {

        }
    }

    private bool TryHandleProjectEventArgs<T>(BuildEventContext buildEventContext, T eventArgs, Action<BuildEventContext, MSBuildProjectLoggingState, T> projectStateEventHandler)
        where T : BuildEventArgs
    {
        if (projects.TryGetValue(buildEventContext, out var projectState))
        {
            projectStateEventHandler(buildEventContext, projectState, eventArgs);
            return true;
        }
        if (orphans.TryGetValue(buildEventContext, out var orphanedEventList))
        {
            orphanedEventList.Add(eventArgs);
            return true;
        }
        return false;
    }

    public void Shutdown() { }

    private void RemoveOrphanedProjects(BuildEventContext eventContext)
    {
        orphans.Remove(eventContext);
    }

    private void UpdateOrphanedProjects(BuildEventContext parentEventContext)
    {
        var parentState = projects[parentEventContext];
        foreach (var orphanedEventList in orphans.Values.Where(o => ctxCmp.Equals(o.First().BuildEventContext, parentEventContext)).ToList())
        {
            BuildEventContext buildEventContext = orphanedEventList.First().BuildEventContext
                ?? BuildEventContext.Invalid;
            MSBuildProjectLoggingState projectState = new(parentState);
            projects.Add(buildEventContext, projectState);
            foreach (var orphanedEvent in orphanedEventList)
            {
                switch (orphanedEvent)
                {
                    case ProjectStartedEventArgs e:
                        HandleProjectStarted(this, e);
                        break;
                    case ProjectFinishedEventArgs e:
                        HandleProjectFinished(this, e);
                        break;
                    case ExternalProjectStartedEventArgs e:
                        HandleExternalProjectStarted(this, e);
                        break;
                    case ExternalProjectFinishedEventArgs e:
                        HandleExternalProjectFinished(this, e);
                        break;
                    case BuildErrorEventArgs e:
                        HandleErrorRaised(this, e);
                        break;
                    case BuildWarningEventArgs e:
                        HandleWarningRaised(this, e);
                        break;
                    case BuildMessageEventArgs e:
                        HandleMessageRaised(this, e);
                        break;
                    case CustomBuildEventArgs e:
                        HandleCustomEventRaised(this, e);
                        break;
                }
            }
            UpdateOrphanedProjects(buildEventContext);
            orphans.Remove(buildEventContext);
        }
    }

    private static BuildEventContext GetExternalProjectEventContext(BuildEventContext eventContext)
    {
        int nodeId = -(eventContext.TargetId + (eventContext.NodeId + 1 << 24));
        int projectContextId = -(eventContext.TaskId + (eventContext.NodeId + 1 << 24));
        return new(nodeId, 0, projectContextId, 0);
    }
}
