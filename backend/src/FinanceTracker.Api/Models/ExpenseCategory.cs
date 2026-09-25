namespace FinanceTracker.Api.Models;

/// <summary>
/// Фиксированный набор категорий. Enum проще отдельной таблицы и сразу даёт
/// строгую валидацию входных данных. В БД хранится строкой (см. AppDbContext),
/// поэтому новые значения можно добавлять в конец без миграции данных.
/// </summary>
public enum ExpenseCategory
{
    Food,
    Transport,
    Housing,
    Utilities,
    Health,
    Entertainment,
    Shopping,
    Education,
    Other
}
