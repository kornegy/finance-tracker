// Должны совпадать с enum Currency на бэкенде (backend/.../Models/Currency.cs).
// Порядок — порядок в списке выбора; первая валюта используется по умолчанию.
export const CURRENCIES = ["CZK", "EUR", "USD", "UAH", "RUB"] as const;

export type Currency = (typeof CURRENCIES)[number];

export const CURRENCY_INFO: Record<Currency, { symbol: string; name: string }> = {
  CZK: { symbol: "Kč", name: "Чешская крона" },
  EUR: { symbol: "€", name: "Евро" },
  USD: { symbol: "$", name: "Доллар США" },
  UAH: { symbol: "₴", name: "Гривна" },
  RUB: { symbol: "₽", name: "Рубль" },
};

export function isCurrency(value: unknown): value is Currency {
  return typeof value === "string" && (CURRENCIES as readonly string[]).includes(value);
}
