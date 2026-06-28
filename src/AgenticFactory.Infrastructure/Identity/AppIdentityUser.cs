using Microsoft.AspNetCore.Identity;

namespace AgenticFactory.Infrastructure.Identity;

public class AppIdentityUser : IdentityUser<Guid>
{
    public Guid OrganizationId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
