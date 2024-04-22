using Microsoft.Build.Framework;

namespace THNETII.MSBuild.Logging;

public class MSBuildForwardingLogger : IForwardingLogger, INodeLogger, ILogger
{
    public IEventRedirector? BuildEventRedirector { get; set; }
    public int NodeId { get; set; }
    public string? Parameters { get; set; }
    public LoggerVerbosity Verbosity { get; set; }

    private void ForwardEvent(object sender, BuildEventArgs e) =>
        BuildEventRedirector?.ForwardEvent(e);

    public void Initialize(IEventSource eventSource) => Initialize(eventSource, -1);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1725: Parameter names should match base declaration")]
    public void Initialize(IEventSource source, int nodeId)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(source);
#else
        _ = source ?? throw new ArgumentNullException(nameof(source));
#endif

        NodeId = nodeId;
        source.ProjectStarted += ForwardEvent;
        source.ProjectFinished += ForwardEvent;
        source.ErrorRaised += ForwardEvent;
        source.WarningRaised += ForwardEvent;
        source.MessageRaised += ForwardEvent;
        source.CustomEventRaised += ForwardEvent;
    }

    public void Shutdown() { }
}
