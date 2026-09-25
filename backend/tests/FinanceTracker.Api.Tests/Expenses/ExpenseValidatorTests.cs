using FinanceTracker.Api.Expenses;
using FinanceTracker.Api.Models;
using FluentAssertions;

namespace FinanceTracker.Api.Tests.Expenses;

public class ExpenseValidatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 15);

    private static ExpenseRequest Valid() => new(250.50m, ExpenseCategory.Food, Today, "Обед");

    [Fact]
    public void Validate_ValidRequest_HasNoErrors()
    {
        ExpenseValidator.Validate(Valid(), Today).Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithoutDateAndNote_HasNoErrors()
    {
        ExpenseValidator.Validate(Valid() with { Date = null, Note = null }, Today).Should().BeEmpty();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("10.555")]
    [InlineData("1000000000.01")]
    public void Validate_InvalidAmount_ReturnsAmountError(string amount)
    {
        var request = Valid() with { Amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture) };

        ExpenseValidator.Validate(request, Today).Should().ContainKey("amount");
    }

    [Fact]
    public void Validate_UndefinedCategory_ReturnsCategoryError()
    {
        var request = Valid() with { Category = (ExpenseCategory)999 };

        ExpenseValidator.Validate(request, Today).Should().ContainKey("category");
    }

    [Theory]
    [InlineData(1999, 12, 31)]
    [InlineData(2026, 9, 17)]
    public void Validate_DateOutOfRange_ReturnsDateError(int year, int month, int day)
    {
        var request = Valid() with { Date = new DateOnly(year, month, day) };

        ExpenseValidator.Validate(request, Today).Should().ContainKey("date");
    }

    [Fact]
    public void Validate_TomorrowIsAllowed_ForTimezonesAheadOfUtc()
    {
        ExpenseValidator.Validate(Valid() with { Date = Today.AddDays(1) }, Today).Should().BeEmpty();
    }

    [Fact]
    public void Validate_TooLongNote_ReturnsNoteError()
    {
        var request = Valid() with { Note = new string('x', 501) };

        ExpenseValidator.Validate(request, Today).Should().ContainKey("note");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  кофе  ", "кофе")]
    public void NormalizeNote_TrimsAndTurnsBlankIntoNull(string? input, string? expected)
    {
        ExpenseValidator.NormalizeNote(input).Should().Be(expected);
    }
}
