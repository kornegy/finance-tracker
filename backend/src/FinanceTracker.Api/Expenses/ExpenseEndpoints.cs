using FinanceTracker.Api.Auth;

namespace FinanceTracker.Api.Expenses;

public static class ExpenseEndpoints
{
    // Защита от слишком тяжёлых запросов истории.
    private const int MaxRangeDays = 366;

    public static void MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        // Авторизация не указана явно: её требует fallback policy из Program.cs.
        var group = app.MapGroup("/api/expenses").WithTags("Expenses");

        group.MapGet("/", List);
        group.MapGet("/summary", Summary);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
    }

    /// <summary>GET /api/expenses?from=2026-09-01&amp;to=2026-09-30. Без параметров — текущий месяц.</summary>
    private static async Task<IResult> List(DateOnly? from, DateOnly? to, HttpContext http, ExpenseService service, CancellationToken ct)
    {
        var today = service.Today;
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? start.AddMonths(1).AddDays(-1);

        if (end < start || end.DayNumber - start.DayNumber > MaxRangeDays)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["range"] = [$"Период должен быть от 1 до {MaxRangeDays} дней."],
            });

        return Results.Ok(await service.ListAsync(http.User.GetUserId(), start, end, ct));
    }

    /// <summary>GET /api/expenses/summary?year=2026&amp;month=9. Без параметров — текущий месяц.</summary>
    private static async Task<IResult> Summary(int? year, int? month, HttpContext http, ExpenseService service, CancellationToken ct)
    {
        var today = service.Today;
        var y = year ?? today.Year;
        var m = month ?? today.Month;

        if (y is < 2000 or > 2100 || m is < 1 or > 12)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["month"] = ["Неверный месяц."] });

        return Results.Ok(await service.GetMonthlySummaryAsync(http.User.GetUserId(), y, m, ct));
    }

    private static async Task<IResult> Get(Guid id, HttpContext http, ExpenseService service, CancellationToken ct) =>
        await service.GetAsync(http.User.GetUserId(), id, ct) is { } expense
            ? Results.Ok(expense)
            : Results.NotFound();

    private static async Task<IResult> Create(ExpenseRequest request, HttpContext http, ExpenseService service, CancellationToken ct)
    {
        var errors = ExpenseValidator.Validate(request, service.Today);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var created = await service.CreateAsync(http.User.GetUserId(), request, ct);
        return Results.Created($"/api/expenses/{created.Id}", created);
    }

    private static async Task<IResult> Update(Guid id, ExpenseRequest request, HttpContext http, ExpenseService service, CancellationToken ct)
    {
        var errors = ExpenseValidator.Validate(request, service.Today);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        return await service.UpdateAsync(http.User.GetUserId(), id, request, ct) is { } updated
            ? Results.Ok(updated)
            : Results.NotFound();
    }

    private static async Task<IResult> Delete(Guid id, HttpContext http, ExpenseService service, CancellationToken ct) =>
        await service.DeleteAsync(http.User.GetUserId(), id, ct)
            ? Results.NoContent()
            : Results.NotFound();
}
