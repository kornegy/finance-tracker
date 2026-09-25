namespace FinanceTracker.Api.Models;

/// <summary>
/// Пользователь приложения. Первичный ключ — Telegram user id:
/// он уникален и неизменен, поэтому отдельный суррогатный ключ не нужен.
/// </summary>
public class User
{
    public long Id { get; set; }

    public required string FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Username { get; set; }
    public string? LanguageCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastLoginAt { get; set; }

    public List<Expense> Expenses { get; set; } = [];
}
