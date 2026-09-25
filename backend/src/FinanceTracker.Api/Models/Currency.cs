namespace FinanceTracker.Api.Models;

/// <summary>
/// Поддерживаемые валюты (коды ISO 4217). Хранятся в БД строкой.
/// Первое значение — валюта по умолчанию, если клиент её не прислал.
/// Чтобы добавить валюту, допишите её сюда и в frontend/src/currencies.ts.
/// </summary>
public enum Currency
{
    CZK,
    EUR,
    USD,
    UAH,
    RUB
}
