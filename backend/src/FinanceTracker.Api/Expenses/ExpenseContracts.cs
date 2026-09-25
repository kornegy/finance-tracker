using FinanceTracker.Api.Models;

namespace FinanceTracker.Api.Expenses;

/// <summary>
/// Тело запроса на создание и изменение расхода. Date не указана — берётся сегодняшний день,
/// Currency не указана — первая валюта из enum (CZK).
/// </summary>
public record ExpenseRequest(decimal Amount, ExpenseCategory Category, DateOnly? Date, string? Note, Currency Currency = Currency.CZK);

public record ExpenseResponse(Guid Id, decimal Amount, Currency Currency, ExpenseCategory Category, DateOnly Date, string? Note, DateTimeOffset CreatedAt)
{
    public static ExpenseResponse From(Expense e) => new(e.Id, e.Amount, e.Currency, e.Category, e.Date, e.Note, e.CreatedAt);
}

public record CategoryTotal(ExpenseCategory Category, decimal Total, int Count);

/// <summary>Итоги одной валюты. Разные валюты не складываются и не конвертируются.</summary>
public record CurrencyTotal(Currency Currency, decimal Total, int Count, IReadOnlyList<CategoryTotal> Categories);

public record MonthlySummary(int Year, int Month, IReadOnlyList<CurrencyTotal> Currencies);
