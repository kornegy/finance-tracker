# Finance Tracker: Telegram Mini App

Учёт личных расходов прямо в Telegram. Главный экран — форма «Новый расход» (сумма → категория → «Сохранить»), вторая вкладка — история и статистика за месяц.

**Как запустить в Telegram: [DEPLOY.md](DEPLOY.md).**

## Стек

- **Бэкенд:** ASP.NET Core 8 (Minimal API), EF Core 8 (Code-First), PostgreSQL
- **Безопасность:** проверка подписи Telegram `initData` (HMAC-SHA256), затем JWT
- **Фронтенд:** React 19, TypeScript, Tailwind CSS 4, `@twa-dev/sdk`, цвета из темы Telegram
- **Тесты:** xUnit, Moq, FluentAssertions (53 теста: авторизация, валидация, бизнес-логика, HTTP)
- **Деплой:** один Docker-образ (API и статика фронтенда), Blueprint для Render

## Структура

```
backend/
  src/FinanceTracker.Api/
    Program.cs                 # DI, EF Core, JWT, CORS, rate limiting, заголовки безопасности
    Models/                    # User, Expense, ExpenseCategory
    Data/                      # AppDbContext, миграции, разбор postgres:// URL
    Auth/                      # TelegramInitDataValidator, JwtTokenService, /api/auth/*
    Expenses/                  # ExpenseService, ExpenseValidator, /api/expenses/*
  tests/FinanceTracker.Api.Tests/
frontend/
  src/components/              # AuthGate, ExpenseForm, HistoryView
  src/api.ts                   # клиент API с JWT и повторным входом
Dockerfile                     # сборка фронтенда и бэкенда в один образ
render.yaml                    # деплой на Render одной кнопкой
docker-compose.yml             # PostgreSQL (и всё приложение) локально
```

## API

Всё, кроме `/health` и `/api/auth/telegram`, требует заголовок `Authorization: Bearer <jwt>`.

| Метод | Путь | Что делает |
|---|---|---|
| POST | `/api/auth/telegram` | `{ initData }` → `{ accessToken, expiresAt, user }` |
| GET | `/api/auth/me` | текущий пользователь |
| GET | `/api/expenses?from=YYYY-MM-DD&to=YYYY-MM-DD` | расходы за период (по умолчанию текущий месяц) |
| GET | `/api/expenses/summary?year=2026&month=9` | итог за месяц по категориям |
| GET | `/api/expenses/{id}` | один расход |
| POST | `/api/expenses` | `{ amount, category, date?, note? }` → 201 |
| PUT | `/api/expenses/{id}` | изменить |
| DELETE | `/api/expenses/{id}` | удалить → 204 |

Категории: `Food`, `Transport`, `Housing`, `Utilities`, `Health`, `Entertainment`, `Shopping`, `Education`, `Other`.

## Безопасность

- Подпись `initData` проверяется по [алгоритму Telegram](https://core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app) со сравнением за постоянное время. Данные старше 1 часа отклоняются.
- JWT подписан HMAC-SHA256, срок жизни 12 часов. На фронтенде токен хранится только в памяти.
- `UserId` всегда берётся из токена. Чужой расход для пользователя «не существует» (404).
- Все запросы к БД идут через EF Core LINQ и параметризуются, поэтому SQL-инъекции исключены.
- Строгая валидация: сумма > 0, не больше 2 знаков после запятой, категория только из списка (числа запрещены), дата в разумном диапазоне, заметка до 500 символов.
- XSS: React экранирует вывод, а Content-Security-Policy запрещает сторонние и inline-скрипты.
- На вход действует лимит 10 запросов в минуту с одного IP. Без секретов приложение не стартует.

## Локальная разработка

Нужны .NET 8 SDK, Node.js 22 и Docker (или свой PostgreSQL).

```bash
docker compose up -d postgres

cd backend/src/FinanceTracker.Api
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Database=finance_tracker;Username=finance;Password=finance_dev_password"
dotnet user-secrets set "Telegram:BotToken" "<токен от @BotFather>"
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
dotnet run                      # http://localhost:5252, миграции применяются сами

cd frontend
npm install
npm run dev                     # http://localhost:5173, /api проксируется на бэкенд
```

Вне Telegram фронтенд покажет «Откройте приложение в Telegram», потому что без `initData` вход невозможен. Для проверки в Telegram нужен публичный `https://` адрес: задеплойте приложение ([DEPLOY.md](DEPLOY.md)) или используйте туннель (ngrok, cloudflared).

Всё приложение в Docker: `TELEGRAM_BOT_TOKEN=... JWT_SIGNING_KEY=... docker compose --profile app up --build` → http://localhost:8080.

### Тесты

```bash
cd backend && dotnet test
```

### Миграции

```bash
cd backend
dotnet tool restore
dotnet ef migrations add <Name> -p src/FinanceTracker.Api -o Data/Migrations
```

## Конфигурация

| Переменная | Назначение |
|---|---|
| `ConnectionStrings__Postgres` | `Host=...;Database=...` или `postgresql://user:pass@host/db` |
| `Telegram__BotToken` | токен бота (секрет) |
| `Jwt__SigningKey` | ключ подписи JWT, от 32 символов (секрет) |
| `Database__MigrateOnStartup` | применять миграции при старте (в Docker-образе `true`) |
| `ReverseProxy__Enabled` | доверять `X-Forwarded-*` от хостинга (в Docker-образе `true`) |
| `Cors__AllowedOrigins__0` | нужен, только если фронтенд живёт на другом домене |
| `VITE_CURRENCY` | код валюты для отображения (при сборке фронтенда) |
