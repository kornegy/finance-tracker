using FinanceTracker.Api.Data;
using FinanceTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Api.Auth;

public record TelegramLoginRequest(string InitData);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);

public record UserResponse(long Id, string FirstName, string? LastName, string? Username);

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // Фронтенд отправляет сюда window.Telegram.WebApp.initData как есть и получает JWT.
        group.MapPost("/telegram", LoginWithTelegram)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth);

        group.MapGet("/me", GetCurrentUser);
    }

    private static async Task<IResult> LoginWithTelegram(
        TelegramLoginRequest request,
        ITelegramInitDataValidator validator,
        IJwtTokenService tokenService,
        AppDbContext db,
        TimeProvider timeProvider,
        ILogger<TelegramLoginRequest> logger,
        CancellationToken ct)
    {
        var result = validator.Validate(request.InitData);
        if (!result.IsValid)
        {
            logger.LogWarning("Rejected Telegram login: {Reason}", result.Error);
            // Причину клиенту не раскрываем.
            return Results.Unauthorized();
        }

        var tgUser = result.User!;
        var now = timeProvider.GetUtcNow();

        // Upsert: первый вход создаёт пользователя, следующие обновляют профиль.
        var user = await db.Users.FindAsync([tgUser.Id], ct);
        if (user is null)
        {
            user = new User { Id = tgUser.Id, FirstName = tgUser.FirstName, CreatedAt = now };
            db.Users.Add(user);
        }

        user.FirstName = Truncate(tgUser.FirstName, 256)!;
        user.LastName = Truncate(tgUser.LastName, 256);
        user.Username = Truncate(tgUser.Username, 64);
        user.LanguageCode = Truncate(tgUser.LanguageCode, 16);
        user.LastLoginAt = now;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (db.Entry(user).State == EntityState.Added)
        {
            // Два параллельных первых входа одного пользователя: запись уже создана другим запросом.
            db.ChangeTracker.Clear();
        }

        var token = tokenService.CreateToken(user);
        return Results.Ok(new LoginResponse(token.Token, token.ExpiresAt, ToResponse(user)));
    }

    private static async Task<IResult> GetCurrentUser(HttpContext http, AppDbContext db, CancellationToken ct)
    {
        var userId = http.User.GetUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? Results.Unauthorized() : Results.Ok(ToResponse(user));
    }

    private static UserResponse ToResponse(User u) => new(u.Id, u.FirstName, u.LastName, u.Username);

    private static string? Truncate(string? value, int maxLength) =>
        value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;
}
