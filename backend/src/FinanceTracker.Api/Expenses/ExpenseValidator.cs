using FinanceTracker.Api.Data;
using FinanceTracker.Api.Models;

namespace FinanceTracker.Api.Expenses;

/// <summary>
/// Правила для входных данных расхода. Возвращает ошибки в формате ValidationProblem
/// (поле -> сообщения); пустой словарь означает, что всё в порядке.
/// </summary>
public static class ExpenseValidator
{
    public const decimal MaxAmount = 1_000_000_000m;
    public static readonly DateOnly MinDate = new(2000, 1, 1);

    public static Dictionary<string, string[]> Validate(ExpenseRequest request, DateOnly today)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.Amount <= 0)
            errors["amount"] = ["Сумма должна быть больше нуля."];
        else if (request.Amount > MaxAmount)
            errors["amount"] = ["Сумма слишком большая."];
        else if (decimal.Round(request.Amount, 2) != request.Amount)
            errors["amount"] = ["Не больше двух знаков после запятой."];

        if (!Enum.IsDefined(request.Category))
            errors["category"] = ["Неизвестная категория."];

        // +1 день: у пользователя восточнее UTC "сегодня" может наступить раньше, чем на сервере.
        if (request.Date is { } date && (date < MinDate || date > today.AddDays(1)))
            errors["date"] = ["Дата вне допустимого диапазона."];

        if (request.Note?.Trim().Length > AppDbContext.MaxNoteLength)
            errors["note"] = [$"Заметка длиннее {AppDbContext.MaxNoteLength} символов."];

        return errors;
    }

    /// <summary>Пустая заметка хранится как null, остальное — без пробелов по краям.</summary>
    public static string? NormalizeNote(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}
