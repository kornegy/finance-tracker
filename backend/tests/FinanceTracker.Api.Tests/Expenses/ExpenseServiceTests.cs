using FinanceTracker.Api.Data;
using FinanceTracker.Api.Expenses;
using FinanceTracker.Api.Models;
using FinanceTracker.Api.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Tests.Expenses;

public class ExpenseServiceTests : IDisposable
{
    private const long Alice = 1;
    private const long Bob = 2;
    private static readonly DateOnly Today = DateOnly.FromDateTime(TestClock.Now.UtcDateTime);

    private readonly AppDbContext _db;
    private readonly ExpenseService _service;

    public ExpenseServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _db.Users.AddRange(
            new User { Id = Alice, FirstName = "Alice" },
            new User { Id = Bob, FirstName = "Bob" });
        _db.SaveChanges();

        _service = new ExpenseService(_db, TestClock.At(TestClock.Now));
    }

    public void Dispose() => _db.Dispose();

    private Task<ExpenseResponse> Add(
        long userId, decimal amount, ExpenseCategory category, DateOnly? date = null, string? note = null, Currency currency = Currency.CZK) =>
        _service.CreateAsync(userId, new ExpenseRequest(amount, category, date, note, currency));

    [Fact]
    public async Task Create_WithoutDate_UsesToday_AndNormalizesNote()
    {
        var created = await Add(Alice, 100m, ExpenseCategory.Food, note: "  обед  ");

        created.Date.Should().Be(Today);
        created.Currency.Should().Be(Currency.CZK);
        created.Note.Should().Be("обед");
        created.CreatedAt.Should().Be(TestClock.Now);
        (await _db.Expenses.SingleAsync()).UserId.Should().Be(Alice);
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnExpensesInRange_NewestFirst()
    {
        await Add(Alice, 1m, ExpenseCategory.Food, Today.AddDays(-2));
        await Add(Alice, 2m, ExpenseCategory.Food, Today);
        await Add(Alice, 3m, ExpenseCategory.Food, Today.AddMonths(-1));
        await Add(Bob, 4m, ExpenseCategory.Food, Today);

        var list = await _service.ListAsync(Alice, Today.AddDays(-7), Today);

        list.Select(e => e.Amount).Should().Equal(2m, 1m);
    }

    [Fact]
    public async Task Get_Update_Delete_CannotTouchAnotherUsersExpense()
    {
        var bobs = await Add(Bob, 50m, ExpenseCategory.Transport);
        var update = new ExpenseRequest(1m, ExpenseCategory.Other, null, "взлом");

        (await _service.GetAsync(Alice, bobs.Id)).Should().BeNull();
        (await _service.UpdateAsync(Alice, bobs.Id, update)).Should().BeNull();
        (await _service.DeleteAsync(Alice, bobs.Id)).Should().BeFalse();

        var stored = await _db.Expenses.AsNoTracking().SingleAsync();
        stored.Amount.Should().Be(50m);
        stored.Category.Should().Be(ExpenseCategory.Transport);
    }

    [Fact]
    public async Task Update_ChangesFields_AndKeepsDateWhenNotProvided()
    {
        var created = await Add(Alice, 10m, ExpenseCategory.Food, Today.AddDays(-3), "old");

        var updated = await _service.UpdateAsync(Alice, created.Id, new ExpenseRequest(20m, ExpenseCategory.Health, null, " ", Currency.EUR));

        updated.Should().NotBeNull();
        updated!.Amount.Should().Be(20m);
        updated.Currency.Should().Be(Currency.EUR);
        updated.Category.Should().Be(ExpenseCategory.Health);
        updated.Date.Should().Be(Today.AddDays(-3));
        updated.Note.Should().BeNull();
    }

    [Fact]
    public async Task Delete_RemovesOwnExpense()
    {
        var created = await Add(Alice, 10m, ExpenseCategory.Food);

        (await _service.DeleteAsync(Alice, created.Id)).Should().BeTrue();
        (await _db.Expenses.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task MonthlySummary_GroupsByCategory_WithinMonthBoundaries()
    {
        await Add(Alice, 100m, ExpenseCategory.Food, new DateOnly(2026, 9, 1));
        await Add(Alice, 50.25m, ExpenseCategory.Food, new DateOnly(2026, 9, 30));
        await Add(Alice, 300m, ExpenseCategory.Housing, new DateOnly(2026, 9, 10));
        await Add(Alice, 999m, ExpenseCategory.Food, new DateOnly(2026, 8, 31));   // прошлый месяц
        await Add(Alice, 999m, ExpenseCategory.Food, new DateOnly(2026, 10, 1));   // следующий месяц
        await Add(Bob, 999m, ExpenseCategory.Food, new DateOnly(2026, 9, 5));      // чужой расход

        var summary = await _service.GetMonthlySummaryAsync(Alice, 2026, 9);

        var czk = summary.Currencies.Should().ContainSingle().Subject;
        czk.Currency.Should().Be(Currency.CZK);
        czk.Total.Should().Be(450.25m);
        czk.Count.Should().Be(3);
        czk.Categories.Should().Equal(
            new CategoryTotal(ExpenseCategory.Housing, 300m, 1),
            new CategoryTotal(ExpenseCategory.Food, 150.25m, 2));
    }

    [Fact]
    public async Task MonthlySummary_KeepsCurrenciesSeparate_MostUsedFirst()
    {
        var day = new DateOnly(2026, 9, 10);
        await Add(Alice, 20m, ExpenseCategory.Food, day, currency: Currency.EUR);
        await Add(Alice, 100m, ExpenseCategory.Food, day, currency: Currency.CZK);
        await Add(Alice, 200m, ExpenseCategory.Transport, day, currency: Currency.CZK);

        var summary = await _service.GetMonthlySummaryAsync(Alice, 2026, 9);

        summary.Currencies.Select(c => (c.Currency, c.Total, c.Count)).Should().Equal(
            (Currency.CZK, 300m, 2),
            (Currency.EUR, 20m, 1));
        summary.Currencies[0].Categories.Select(c => c.Category)
            .Should().Equal(ExpenseCategory.Transport, ExpenseCategory.Food);
    }

    [Fact]
    public async Task MonthlySummary_EmptyMonth_ReturnsZero()
    {
        var summary = await _service.GetMonthlySummaryAsync(Alice, 2026, 2);

        summary.Currencies.Should().BeEmpty();
    }
}
