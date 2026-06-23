using OpsForge.Application;
using OpsForge.Domain;

namespace OpsForge.Worker;

public sealed class WorkerCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId { get; private set; }
    public string? Email { get; private set; }
    public string? Role { get; private set; }

    public void SetUser(AppUser user)
    {
        UserId = user.Id;
        Email = user.Email;
        Role = user.Role.ToString();
    }
}
