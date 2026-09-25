using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FinanceTracker.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Telegram id текущего пользователя из JWT. Эндпоинты берут UserId только отсюда,
    /// а не из тела запроса, чтобы нельзя было работать с чужими данными.
    /// </summary>
    public static long GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(sub, out var id)
            ? id
            : throw new InvalidOperationException("Authenticated principal has no valid 'sub' claim.");
    }
}
