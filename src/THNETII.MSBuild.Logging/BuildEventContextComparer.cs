using System.Diagnostics.CodeAnalysis;

using Microsoft.Build.Framework;

namespace THNETII.MSBuild.Logging;

public sealed class BuildEventContextComparer : IEqualityComparer<BuildEventContext>
{
    public static BuildEventContextComparer Instance { get; } = new();

    public bool Equals(BuildEventContext? x, BuildEventContext? y)
    {
        if (ReferenceEquals(x, y)) return true;
        x ??= BuildEventContext.Invalid;
        y ??= BuildEventContext.Invalid;
        return
            x.NodeId == y.NodeId &&
            x.ProjectContextId == y.ProjectContextId
            ;
    }

    public int GetHashCode([DisallowNull] BuildEventContext obj)
    {
        if (obj is null) return GetHashCode(BuildEventContext.Invalid);
        return HashCode.Combine(obj.ProjectContextId, obj.NodeId);
    }

    private BuildEventContextComparer() { }
}
