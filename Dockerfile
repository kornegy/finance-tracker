# Один образ: собранный React-фронтенд раздаётся тем же ASP.NET Core сервером, что и API.

# ---------- 1. Фронтенд ----------
FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
# Код валюты для отображения сумм (RUB, CZK, EUR...). Пусто — без знака валюты.
ARG VITE_CURRENCY=
ENV VITE_CURRENCY=$VITE_CURRENCY
RUN npm run build

# ---------- 2. Бэкенд ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend
WORKDIR /src
COPY backend/src/FinanceTracker.Api/FinanceTracker.Api.csproj backend/src/FinanceTracker.Api/
RUN dotnet restore backend/src/FinanceTracker.Api/FinanceTracker.Api.csproj
COPY backend/src/ backend/src/
RUN dotnet publish backend/src/FinanceTracker.Api/FinanceTracker.Api.csproj -c Release -o /app --no-restore

# ---------- 3. Итоговый образ ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=backend /app ./
COPY --from=frontend /src/frontend/dist ./wwwroot

# Миграции БД применяются при старте; хостинг стоит перед приложением как reverse proxy.
ENV ASPNETCORE_ENVIRONMENT=Production \
    Database__MigrateOnStartup=true \
    ReverseProxy__Enabled=true

USER app
EXPOSE 8080
# Хостинги вроде Render передают порт в переменной PORT; локально по умолчанию 8080.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet FinanceTracker.Api.dll"]
