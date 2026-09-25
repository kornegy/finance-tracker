// Код валюты ISO 4217 (например RUB, CZK, EUR). Задаётся при сборке: VITE_CURRENCY=CZK.
// Если не задан, суммы показываются без знака валюты.
const CURRENCY = import.meta.env.VITE_CURRENCY as string | undefined;

const moneyFormat = new Intl.NumberFormat("ru-RU", {
  maximumFractionDigits: 2,
  ...(CURRENCY ? { style: "currency", currency: CURRENCY } : {}),
});

export const formatMoney = (value: number) => moneyFormat.format(value);

/** Сегодняшняя дата в часовом поясе пользователя, в формате YYYY-MM-DD. */
export function todayIso(): string {
  const now = new Date();
  return toIsoDate(now.getFullYear(), now.getMonth() + 1, now.getDate());
}

export function toIsoDate(year: number, month: number, day: number): string {
  return `${year}-${String(month).padStart(2, "0")}-${String(day).padStart(2, "0")}`;
}

export function lastDayOfMonth(year: number, month: number): number {
  return new Date(year, month, 0).getDate();
}

const dayFormat = new Intl.DateTimeFormat("ru-RU", { day: "numeric", month: "long", weekday: "short" });
const monthFormat = new Intl.DateTimeFormat("ru-RU", { month: "long", year: "numeric" });

export function formatDay(isoDate: string): string {
  if (isoDate === todayIso()) return "Сегодня";
  const [y, m, d] = isoDate.split("-").map(Number);
  return dayFormat.format(new Date(y, m - 1, d));
}

export function formatMonth(year: number, month: number): string {
  const text = monthFormat.format(new Date(year, month - 1, 1)).replace(" г.", "");
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** Принимает "12,5" или "12.50"; возвращает число или null, если ввод некорректен. */
export function parseAmount(input: string): number | null {
  const normalized = input.trim().replace(/\s/g, "").replace(",", ".");
  if (!/^\d{1,10}(\.\d{1,2})?$/.test(normalized)) return null;
  const value = Number(normalized);
  return value > 0 ? value : null;
}
