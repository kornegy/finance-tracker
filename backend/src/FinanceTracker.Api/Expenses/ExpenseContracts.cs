using FinanceTracker.Api.Models;

namespace FinanceTracker.Api.Expenses;

/// <summary>Тело запроса на создание и изменение расхода. Date не указана — берётся сегодняшний день.</summary>
public record ExpenseRequest(decimal Amount, ExpenseCategory Category, DateOnly? Date, string? Note);

public record ExpenseResponse(Guid Id, decimal Amount, ExpenseCategory Category, DateOnly Date, string? Note, DateTimeOffset CreatedAt)
{
    public static ExpenseResponse From(Expense e) => new(e.Id, e.Amount, e.Category, e.Date, e.Note, e.CreatedAt);
}

public record CategoryTotal(ExpenseCategory Category, decimal Total, int Count);

public record MonthlySummary(int Year, int Month, decimal Total, int Count, IReadOnlyList<CategoryTotal> Categories);
