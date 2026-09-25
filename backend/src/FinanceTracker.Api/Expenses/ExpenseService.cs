using FinanceTracker.Api.Data;
using FinanceTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Expenses;

/// <summary>
/// Бизнес-логика расходов. Каждый метод принимает userId из JWT и работает только
/// с записями этого пользователя: чужой расход для него просто "не существует".
/// Входные данные к этому моменту уже проверены ExpenseValidator.
/// Все запросы идут через LINQ, EF Core параметризует их, поэтому SQL-инъекции исключены.
/// </summary>
public class ExpenseService(AppDbContext db, TimeProvider timeProvider)
{
    public const int MaxListSize = 1000;

    public DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    public async Task<IReadOnlyList<ExpenseResponse>> ListAsync(long userId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
        await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.Date >= from && e.Date <= to)
            .OrderByDescending(e => e.Date).ThenByDescending(e => e.CreatedAt)
            .Take(MaxListSize)
            .Select(e => new ExpenseResponse(e.Id, e.Amount, e.Currency, e.Category, e.Date, e.Note, e.CreatedAt))
            .ToListAsync(ct);

    public async Task<ExpenseResponse?> GetAsync(long userId, Guid id, CancellationToken ct = default)
    {
        var expense = await db.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        return expense is null ? null : ExpenseResponse.From(expense);
    }

    public async Task<ExpenseResponse> CreateAsync(long userId, ExpenseRequest request, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = request.Amount,
            Currency = request.Currency,
            Category = request.Category,
            Date = request.Date ?? Today,
            Note = ExpenseValidator.NormalizeNote(request.Note),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        return ExpenseResponse.From(expense);
    }

    public async Task<ExpenseResponse?> UpdateAsync(long userId, Guid id, ExpenseRequest request, CancellationToken ct = default)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (expense is null)
            return null;

        expense.Amount = request.Amount;
        expense.Currency = request.Currency;
        expense.Category = request.Category;
        expense.Date = request.Date ?? expense.Date;
        expense.Note = ExpenseValidator.NormalizeNote(request.Note);
        expense.UpdatedAt = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(ct);
        return ExpenseResponse.From(expense);
    }

    public async Task<bool> DeleteAsync(long userId, Guid id, CancellationToken ct = default)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (expense is null)
            return false;

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Итоги за месяц отдельно по каждой валюте, внутри — по категориям от самой затратной.
    /// Валюты идут от самой частой, так что основная валюта пользователя первая.
    /// </summary>
    public async Task<MonthlySummary> GetMonthlySummaryAsync(long userId, int year, int month, CancellationToken ct = default)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var rows = await db.Expenses.AsNoTracking()
            .Where(e => e.UserId == userId && e.Date >= from && e.Date <= to)
            .GroupBy(e => new { e.Currency, e.Category })
            .Select(g => new { g.Key.Currency, g.Key.Category, Total = g.Sum(e => e.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var currencies = rows
            .GroupBy(r => r.Currency)
            .Select(g => new CurrencyTotal(
                g.Key,
                g.Sum(r => r.Total),
                g.Sum(r => r.Count),
                g.Select(r => new CategoryTotal(r.Category, r.Total, r.Count))
                    .OrderByDescending(c => c.Total).ThenBy(c => c.Category)
                    .ToList()))
            .OrderByDescending(c => c.Count).ThenBy(c => c.Currency)
            .ToList();

        return new MonthlySummary(year, month, currencies);
    }
}
