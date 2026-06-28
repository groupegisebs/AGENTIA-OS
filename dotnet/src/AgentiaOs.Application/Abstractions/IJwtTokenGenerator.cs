using AgentiaOs.Domain.Entities;

namespace AgentiaOs.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string CreateToken(User user);
}
