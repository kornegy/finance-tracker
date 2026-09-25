namespace FinanceTracker.Api.Models;

public class Expense
{
    public Guid Id { get; set; }

    public long UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Сумма расхода, всегда положительная. Хранится как numeric(12,2).</summary>
    public decimal Amount { get; set; }

    public Currency Currency { get; set; }

    public ExpenseCategory Category { get; set; }

    /// <summary>Календарный день расхода (без времени и часового пояса).</summary>
    public DateOnly Date { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
