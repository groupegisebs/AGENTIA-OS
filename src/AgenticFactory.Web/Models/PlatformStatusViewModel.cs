namespace AgenticFactory.Web.Models;

public sealed class PlatformStatusViewModel
{
    public string Status { get; init; } = "Offline";
    public string Label { get; init; } = "Hors ligne";
    public int NodeCount { get; init; }

    public static PlatformStatusViewModel Offline { get; } = new();

    public string CssModifier => Status switch
    {
        "Healthy" => "healthy",
        "Degraded" => "degraded",
        _ => "offline"
    };
}
