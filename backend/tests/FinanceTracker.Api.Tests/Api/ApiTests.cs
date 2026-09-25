using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinanceTracker.Api.Auth;
using FinanceTracker.Api.Expenses;
using FinanceTracker.Api.Tests.TestSupport;
using FluentAssertions;

namespace FinanceTracker.Api.Tests.Api;

/// <summary>Сквозные проверки через HTTP: авторизация, CRUD и изоляция пользователей.</summary>
public class ApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> LoginAs(long userId)
    {
        var client = _factory.CreateClient();
        var initData = InitDataBuilder.Build(DateTimeOffset.UtcNow, userId);

        var response = await client.PostAsJsonAsync("/api/auth/telegram", new { initData });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    [Theory]
    [InlineData("/api/expenses")]
    [InlineData("/api/expenses/summary")]
    [InlineData("/api/auth/me")]
    public async Task ProtectedEndpoints_WithoutToken_Return401(string url)
    {
        var response = await _factory.CreateClient().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithForgedInitData_Returns401()
    {
        var forged = InitDataBuilder.Build(DateTimeOffset.UtcNow, botToken: "1:wrong");

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/telegram", new { initData = forged });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpenseLifecycle_CreateListSummaryUpdateDelete()
    {
        var client = await LoginAs(1001);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var create = await client.PostAsJsonAsync("/api/expenses", new { amount = 120.5m, category = "Food", note = "обед" });
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await create.Content.ReadFromJsonAsync<ExpenseResponse>(Json.Options))!;
        created.Date.Should().Be(today);

        var list = await client.GetFromJsonAsync<List<ExpenseResponse>>("/api/expenses", Json.Options);
        list.Should().ContainSingle(e => e.Id == created.Id);

        var summary = await client.GetFromJsonAsync<MonthlySummary>("/api/expenses/summary", Json.Options);
        summary!.Total.Should().Be(120.5m);

        var update = await client.PutAsJsonAsync($"/api/expenses/{created.Id}", new { amount = 99m, category = "Transport" });
        update.StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.DeleteAsync($"/api/expenses/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/expenses/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AnotherUser_CannotSeeOrDeleteExpense()
    {
        var owner = await LoginAs(2001);
        var intruder = await LoginAs(2002);

        var create = await owner.PostAsJsonAsync("/api/expenses", new { amount = 10m, category = "Health" });
        var created = (await create.Content.ReadFromJsonAsync<ExpenseResponse>(Json.Options))!;

        (await intruder.GetAsync($"/api/expenses/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await intruder.DeleteAsync($"/api/expenses/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await owner.GetAsync($"/api/expenses/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("""{ "amount": -5, "category": "Food" }""")]
    [InlineData("""{ "amount": 5, "category": "Casino" }""")]
    [InlineData("""{ "amount": 5, "category": 999 }""")]
    [InlineData("""{ "amount": 5, "category": "Food", "date": "1990-01-01" }""")]
    [InlineData("""not json""")]
    public async Task CreateExpense_WithInvalidInput_Returns400(string body)
    {
        var client = await LoginAs(3001);

        var response = await client.PostAsync("/api/expenses", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
