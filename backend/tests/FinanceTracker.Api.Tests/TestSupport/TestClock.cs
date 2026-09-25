using Moq;

namespace FinanceTracker.Api.Tests.TestSupport;

public static class TestClock
{
    public static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    /// <summary>TimeProvider с зафиксированным временем, чтобы тесты не зависели от часов машины.</summary>
    public static TimeProvider At(DateTimeOffset now)
    {
        var mock = new Mock<TimeProvider>();
        mock.Setup(t => t.GetUtcNow()).Returns(now);
        return mock.Object;
    }
}
