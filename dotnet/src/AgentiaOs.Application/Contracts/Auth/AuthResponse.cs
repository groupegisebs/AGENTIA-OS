namespace AgentiaOs.Application.Contracts.Auth;

public sealed record AuthResponse(Guid UserId, string Email, string DisplayName, string Token);
