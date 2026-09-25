using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FinanceTracker.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinanceTracker.Api.Auth;

public record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface IJwtTokenService
{
    AccessToken CreateToken(User user);
}

public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateToken(User user)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now + _options.Lifetime;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(CreateSigningKey(_options), SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public static SymmetricSecurityKey CreateSigningKey(JwtOptions options) =>
        new(Encoding.UTF8.GetBytes(options.SigningKey));
}
