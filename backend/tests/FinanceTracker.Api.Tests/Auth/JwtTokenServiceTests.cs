using System.IdentityModel.Tokens.Jwt;
using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Models;
using FinanceTracker.Api.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinanceTracker.Api.Tests.Auth;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "test-signing-key-that-is-long-enough-123",
        Lifetime = TimeSpan.FromHours(12),
    };

    private static readonly User User = new() { Id = 42, FirstName = "Иван" };

    private static TokenValidationParameters ValidationParameters(JwtOptions options, DateTimeOffset now) => new()
    {
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = JwtTokenService.CreateSigningKey(options),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        LifetimeValidator = (notBefore, expires, _, _) => notBefore <= now.UtcDateTime && now.UtcDateTime < expires,
    };

    private static JwtTokenService CreateService() =>
        new(Microsoft.Extensions.Options.Options.Create(Options), TestClock.At(TestClock.Now));

    [Fact]
    public void CreateToken_ProducesTokenThatValidates_WithUserIdInSub()
    {
        var token = CreateService().CreateToken(User);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token.Token, ValidationParameters(Options, TestClock.Now), out _);

        principal.GetUserId().Should().Be(42);
        token.ExpiresAt.Should().Be(TestClock.Now + Options.Lifetime);
    }

    [Fact]
    public void CreateToken_IsRejected_AfterExpiry()
    {
        var token = CreateService().CreateToken(User);
        var later = TestClock.Now + Options.Lifetime + TimeSpan.FromSeconds(1);

        var act = () => new JwtSecurityTokenHandler().ValidateToken(token.Token, ValidationParameters(Options, later), out _);

        act.Should().Throw<SecurityTokenInvalidLifetimeException>();
    }

    [Fact]
    public void CreateToken_IsRejected_WithDifferentSigningKey()
    {
        var token = CreateService().CreateToken(User);
        var otherKey = new JwtOptions
        {
            Issuer = Options.Issuer,
            Audience = Options.Audience,
            SigningKey = "another-signing-key-that-is-long-enough-456",
        };

        var act = () => new JwtSecurityTokenHandler().ValidateToken(token.Token, ValidationParameters(otherKey, TestClock.Now), out _);

        act.Should().Throw<SecurityTokenSignatureKeyNotFoundException>();
    }
}
