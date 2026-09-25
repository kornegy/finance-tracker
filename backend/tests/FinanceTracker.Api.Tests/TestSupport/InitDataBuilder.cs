using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FinanceTracker.Api.Tests.TestSupport;

/// <summary>Собирает initData и подписывает её так же, как это делает Telegram.</summary>
public static class InitDataBuilder
{
    public const string BotToken = "123456:TEST-bot-token";

    public static Dictionary<string, string> DefaultFields(DateTimeOffset authDate, long userId = 42) => new()
    {
        ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(),
        ["query_id"] = "AAHdF6IQAAAAAN0XohDhrOrc",
        ["user"] = JsonSerializer.Serialize(new { id = userId, first_name = "Иван", username = "ivan", language_code = "ru" }),
    };

    public static string Build(DateTimeOffset authDate, long userId = 42, string botToken = BotToken) =>
        Sign(DefaultFields(authDate, userId), botToken);

    public static string Sign(IDictionary<string, string> fields, string botToken = BotToken)
    {
        var dataCheckString = string.Join('\n', fields.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}={f.Value}"));
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var hash = Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString))).ToLowerInvariant();
        return ToQuery(fields.Append(new("hash", hash)));
    }

    public static string ToQuery(IEnumerable<KeyValuePair<string, string>> fields) =>
        string.Join('&', fields.Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}"));
}
