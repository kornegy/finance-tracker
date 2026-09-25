using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Api.Tests.Auth;

public class TelegramInitDataValidatorTests
{
    private static readonly DateTimeOffset Now = TestClock.Now;

    private static TelegramInitDataValidator CreateValidator(string botToken = InitDataBuilder.BotToken) =>
        new(Options.Create(new TelegramOptions { BotToken = botToken, InitDataMaxAge = TimeSpan.FromHours(1) }),
            TestClock.At(Now));

    [Fact]
    public void Validate_ValidInitData_ReturnsUser()
    {
        var result = CreateValidator().Validate(InitDataBuilder.Build(Now.AddMinutes(-5)));

        result.IsValid.Should().BeTrue(result.Error);
        result.User.Should().BeEquivalentTo(new TelegramUser(42, "Иван", null, "ivan", "ru"));
    }

    [Fact]
    public void Validate_TamperedField_IsRejected()
    {
        var initData = InitDataBuilder.Build(Now).Replace("ivan", "petr");

        CreateValidator().Validate(initData).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_TamperedUserId_IsRejected()
    {
        // Злоумышленник подписывает свои данные, а потом меняет id на чужой.
        var initData = InitDataBuilder.Build(Now, userId: 42).Replace("%3A42%2C", "%3A43%2C");

        initData.Should().Contain("%3A43%2C");
        CreateValidator().Validate(initData).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_SignedWithAnotherBotToken_IsRejected()
    {
        var initData = InitDataBuilder.Build(Now, botToken: "999:other-bot");

        CreateValidator().Validate(initData).Error.Should().Be("hash does not match.");
    }

    [Fact]
    public void Validate_ExpiredAuthDate_IsRejected()
    {
        var result = CreateValidator().Validate(InitDataBuilder.Build(Now.AddHours(-2)));

        result.IsValid.Should().BeFalse();
        result.Error.Should().Be("initData has expired.");
    }

    [Fact]
    public void Validate_AuthDateInFuture_IsRejected()
    {
        var result = CreateValidator().Validate(InitDataBuilder.Build(Now.AddMinutes(10)));

        result.Error.Should().Be("auth_date is in the future.");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("user=%7B%7D&auth_date=1")]
    [InlineData("hash=zz&auth_date=1")]
    [InlineData("hash=abc")]
    public void Validate_MissingOrMalformedHash_IsRejected(string? initData)
    {
        CreateValidator().Validate(initData).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_DuplicateKey_IsRejected()
    {
        var initData = InitDataBuilder.Build(Now) + "&auth_date=1";

        CreateValidator().Validate(initData).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_TooLongInput_IsRejected()
    {
        var initData = InitDataBuilder.Build(Now) + "&x=" + new string('a', 5000);

        CreateValidator().Validate(initData).Error.Should().Be("initData is too long.");
    }

    [Fact]
    public void Validate_SignedButWithoutUser_IsRejected()
    {
        var fields = InitDataBuilder.DefaultFields(Now);
        fields.Remove("user");

        CreateValidator().Validate(InitDataBuilder.Sign(fields)).Error.Should().Be("user is missing.");
    }

    [Fact]
    public void Validate_SignedButUserIsNotJson_IsRejected()
    {
        var fields = InitDataBuilder.DefaultFields(Now);
        fields["user"] = "not-json";

        CreateValidator().Validate(InitDataBuilder.Sign(fields)).Error.Should().Be("user is malformed.");
    }

    [Fact]
    public void Validate_ExtraSignedFields_AreIncludedInHash()
    {
        // Новые поля Telegram (например signature, chat_instance) участвуют в подписи — их нельзя выбрасывать.
        var fields = InitDataBuilder.DefaultFields(Now);
        fields["signature"] = "abc";
        fields["chat_instance"] = "-123";

        CreateValidator().Validate(InitDataBuilder.Sign(fields)).IsValid.Should().BeTrue();
    }
}
