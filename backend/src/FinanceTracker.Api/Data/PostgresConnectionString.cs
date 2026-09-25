using Npgsql;

namespace FinanceTracker.Api.Data;

public static class PostgresConnectionString
{
    /// <summary>
    /// Хостинги (Render, Railway, Heroku, Neon) выдают строку подключения в виде URL:
    /// postgresql://user:password@host:5432/dbname?sslmode=require.
    /// Npgsql такой формат не понимает, поэтому переводим его в "Host=...;Username=...".
    /// Обычная строка вида "Host=...;..." возвращается без изменений.
    /// </summary>
    public static string Normalize(string connectionString)
    {
        if (!connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return connectionString;

        var uri = new Uri(connectionString);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        if (Enum.TryParse<SslMode>(query["sslmode"], ignoreCase: true, out var sslMode))
            builder.SslMode = sslMode;

        return builder.ConnectionString;
    }
}
