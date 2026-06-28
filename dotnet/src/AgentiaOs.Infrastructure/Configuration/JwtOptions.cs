namespace AgentiaOs.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AgentiaOs";
    public string Audience { get; set; } = "AgentiaOs.Client";
    public string SigningKey { get; set; } = "ReplaceWithLongSecureKeyForPhase1Only123!";
    public int ExpirationMinutes { get; set; } = 60;
}
