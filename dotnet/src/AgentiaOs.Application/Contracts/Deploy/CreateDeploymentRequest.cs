namespace AgentiaOs.Application.Contracts.Deploy;

public sealed record CreateDeploymentRequest(Guid ConversationId, string TargetEnvironment);
