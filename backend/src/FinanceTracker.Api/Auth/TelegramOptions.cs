using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Api.Auth;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    /// <summary>Токен бота от @BotFather. Только через user-secrets или переменную окружения Telegram__BotToken.</summary>
    [Required]
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Сколько initData считается свежей после auth_date. Защищает от повторного использования старых данных.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan InitDataMaxAge { get; set; } = TimeSpan.FromHours(1);
}
