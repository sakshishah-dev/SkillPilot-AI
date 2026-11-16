using System.Security.Claims;

namespace SkillPilot.Api.Security;

public static class CurrentUser
{
    public static Guid Id(this ClaimsPrincipal p)
    {
        var v = p.FindFirstValue(ClaimTypes.NameIdentifier) ?? p.FindFirstValue("sub");
        return Guid.TryParse(v, out var id) ? id : throw new UnauthorizedAccessException();
    }
}
