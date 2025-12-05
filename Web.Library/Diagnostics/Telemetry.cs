using System.Diagnostics;

namespace Web.Library.Diagnostics;

public static class Telemetry
{
    public const string ActivitySourceName = "Web.Library";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal) => ActivitySource.StartActivity(name, kind);
}