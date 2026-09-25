using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Auth;

/// <summary>Пользователь из поля <c>user</c> в initData.</summary>
public record TelegramUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("language_code")] string? LanguageCode);

public record InitDataValidationResult(bool IsValid, TelegramUser? User, string? Error)
{
    public static InitDataValidationResult Success(TelegramUser user) => new(true, user, null);
    public static InitDataValidationResult Failure(string error) => new(false, null, error);
}

public interface ITelegramInitDataValidator
{
    InitDataValidationResult Validate(string? initData);
}

/// <summary>
/// Проверяет подпись initData по алгоритму Telegram:
/// https://core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app
///
/// 1. data_check_string = все поля кроме hash, отсортированные по ключу, в виде "key=value", через "\n".
/// 2. secret_key = HMAC_SHA256(key: "WebAppData", data: bot_token).
/// 3. Ожидаемый hash = hex(HMAC_SHA256(key: secret_key, data: data_check_string)).
/// Плюс проверка свежести auth_date, чтобы перехваченные initData нельзя было использовать вечно.
/// </summary>
public class TelegramInitDataValidator : ITelegramInitDataValidator
{
    // initData от Telegram занимает ~1 КБ; лимит отсекает мусор до вычисления HMAC.
    private const int MaxInitDataLength = 4096;

    // Небольшой допуск на расхождение часов между серверами Telegram и нашим.
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(1);

    private readonly byte[] _secretKey;
    private readonly TimeSpan _maxAge;
    private readonly TimeProvider _timeProvider;

    public TelegramInitDataValidator(IOptions<TelegramOptions> options, TimeProvider timeProvider)
    {
        _secretKey = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(options.Value.BotToken));
        _maxAge = options.Value.InitDataMaxAge;
        _timeProvider = timeProvider;
    }

    public InitDataValidationResult Validate(string? initData)
    {
        if (string.IsNullOrWhiteSpace(initData))
            return InitDataValidationResult.Failure("initData is empty.");
        if (initData.Length > MaxInitDataLength)
            return InitDataValidationResult.Failure("initData is too long.");

        var fields = QueryHelpers.ParseQuery(initData);

        // Повторяющиеся ключи Telegram не присылает — такая строка подделана.
        if (fields.Any(f => f.Value.Count != 1))
            return InitDataValidationResult.Failure("initData contains duplicate or empty keys.");

        if (!fields.TryGetValue("hash", out var hashValues) || !TryParseHex(hashValues.ToString(), out var receivedHash))
            return InitDataValidationResult.Failure("hash is missing or malformed.");

        var dataCheckString = string.Join('\n', fields
            .Where(f => f.Key != "hash")
            .OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => $"{f.Key}={f.Value}"));

        var expectedHash = HMACSHA256.HashData(_secretKey, Encoding.UTF8.GetBytes(dataCheckString));

        // Сравнение за постоянное время, чтобы не было утечки через тайминг.
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, receivedHash))
            return InitDataValidationResult.Failure("hash does not match.");

        if (!fields.TryGetValue("auth_date", out var authDateValue)
            || !long.TryParse(authDateValue, NumberStyles.None, CultureInfo.InvariantCulture, out var authDateUnix))
            return InitDataValidationResult.Failure("auth_date is missing or malformed.");

        var authDate = DateTimeOffset.FromUnixTimeSeconds(authDateUnix);
        var now = _timeProvider.GetUtcNow();
        if (authDate > now + AllowedClockSkew)
            return InitDataValidationResult.Failure("auth_date is in the future.");
        if (now - authDate > _maxAge)
            return InitDataValidationResult.Failure("initData has expired.");

        if (!fields.TryGetValue("user", out var userJson))
            return InitDataValidationResult.Failure("user is missing.");

        TelegramUser? user;
        try
        {
            user = JsonSerializer.Deserialize<TelegramUser>(userJson.ToString());
        }
        catch (JsonException)
        {
            return InitDataValidationResult.Failure("user is malformed.");
        }

        if (user is null || user.Id <= 0 || string.IsNullOrWhiteSpace(user.FirstName))
            return InitDataValidationResult.Failure("user is malformed.");

        return InitDataValidationResult.Success(user);
    }

    private static bool TryParseHex(string hex, out byte[] bytes)
    {
        bytes = [];
        // SHA-256 в hex — ровно 64 символа.
        if (hex.Length != 64)
            return false;
        try
        {
            bytes = Convert.FromHexString(hex);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
