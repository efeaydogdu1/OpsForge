using System.Security.Claims;
using OpsForge.Application;

namespace OpsForge.Api;

public sealed class HttpCurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    public Guid? UserId
    {
        get
        {
            var value = accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Email => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
    public string? Role => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
}
