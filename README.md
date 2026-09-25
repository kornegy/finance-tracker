# Finance Tracker — Telegram Mini App

Учёт личных расходов внутри Telegram. Бэкенд: ASP.NET Core 8 + EF Core + PostgreSQL.

## Статус

- [x] Шаг 1 — модели, `AppDbContext`, PostgreSQL, первая миграция
- [x] Шаг 2 — валидация Telegram `initData`, выдача и проверка JWT
- [ ] Шаг 3 — CRUD расходов и сводка за месяц
- [ ] Шаг 4 — тесты (xUnit, Moq, FluentAssertions)
- [ ] Шаг 5 — фронтенд (React + TypeScript + Tailwind + `@twa-dev/sdk`)

## Структура

```
backend/
  FinanceTracker.sln
  .config/dotnet-tools.json         # локальный dotnet-ef
  src/FinanceTracker.Api/
    Program.cs                       # DI, EF Core, JWT, CORS, rate limiting
    Models/                          # User, Expense, ExpenseCategory
    Data/AppDbContext.cs             # конфигурация схемы
    Data/Migrations/                 # миграции Code-First
    Auth/TelegramInitDataValidator.cs# проверка подписи initData (HMAC-SHA256)
    Auth/JwtTokenService.cs          # выпуск JWT
    Auth/AuthEndpoints.cs            # POST /api/auth/telegram, GET /api/auth/me
docker-compose.yml                   # локальный PostgreSQL
```

## Быстрый старт

Нужны .NET 8 SDK и Docker (или свой PostgreSQL 14+).

```bash
# 1. База данных
docker compose up -d

# 2. Секреты (не кладите их в appsettings.json и в git)
cd backend/src/FinanceTracker.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=finance_tracker;Username=finance;Password=finance_dev_password"
dotnet user-secrets set "Telegram:BotToken" "<токен от @BotFather>"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"

# 3. Запуск (в Development миграции применяются автоматически)
dotnet run
# -> http://localhost:5252/health
```

Без `Telegram:BotToken` или `Jwt:SigningKey` (минимум 32 символа) приложение не стартует: так ошибка конфигурации видна сразу, а не на первом запросе.

### Продакшен

Секреты задаются переменными окружения: `ConnectionStrings__Postgres`, `Telegram__BotToken`, `Jwt__SigningKey`, `Cors__AllowedOrigins__0=https://ваш-фронтенд`.
Миграции применяются явно: `dotnet ef database update` или SQL-скрипт `dotnet ef migrations script --idempotent`.

Если API стоит за reverse proxy (nginx, Caddy), включите `UseForwardedHeaders`, иначе rate limiting будет видеть IP прокси вместо IP клиента.

### Миграции

```bash
cd backend
dotnet tool restore
dotnet ef migrations add <Name> -p src/FinanceTracker.Api -o Data/Migrations
dotnet ef database update -p src/FinanceTracker.Api
```

## Как работает вход

1. Mini App берёт `window.Telegram.WebApp.initData` (строка, подписанная Telegram) и отправляет её как есть:
   `POST /api/auth/telegram` с телом `{ "initData": "<строка>" }`.
2. Бэкенд проверяет HMAC-подпись токеном бота (алгоритм из [документации Telegram](https://core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app)), сравнение за постоянное время, и что `auth_date` не старше `Telegram:InitDataMaxAge` (по умолчанию 1 час).
3. Пользователь создаётся или обновляется в БД, в ответ приходит JWT (`accessToken`, `expiresAt`, `user`).
4. Дальше все запросы идут с заголовком `Authorization: Bearer <accessToken>`.

Все эндпоинты по умолчанию требуют JWT (fallback policy); открыты только `/health` и `/api/auth/telegram`. Вход ограничен 10 запросами в минуту с одного IP. `UserId` всегда берётся из токена (`User.GetUserId()`), а не из тела запроса.

## Модель данных

| Таблица    | Поля |
|------------|------|
| `users`    | `Id` (Telegram id, PK), `FirstName`, `LastName`, `Username`, `LanguageCode`, `CreatedAt`, `LastLoginAt` |
| `expenses` | `Id` (uuid), `UserId` (FK, каскадное удаление), `Amount` numeric(12,2) > 0, `Category` (строка из enum), `Date` (date), `Note` ≤ 500, `CreatedAt`, `UpdatedAt` |

Индекс `(UserId, Date)` покрывает основные запросы: история и сводка за месяц.
Категории — фиксированный enum `ExpenseCategory`, хранится строкой, поэтому его легко расширить.
