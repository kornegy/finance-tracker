using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Симметричный ключ HMAC-SHA256, минимум 32 символа. Только через user-secrets или Jwt__SigningKey.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = string.Empty;

    [Range(typeof(TimeSpan), "00:05:00", "7.00:00:00")]
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(12);
}
