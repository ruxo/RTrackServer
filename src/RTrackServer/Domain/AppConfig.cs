namespace RTrackServer.Domain;

public sealed class AppConfig
{
    public string BasePath { get; set; } = "/";
    public int TrackerPort { get; set; } = 9999;
    public int ClientTimeout { get; set; } = 10;
}