import WebApp from "./webapp";
import type { Category } from "./categories";
import type { Currency } from "./currencies";

export type Expense = {
  id: string;
  amount: number;
  currency: Currency;
  category: Category;
  date: string; // YYYY-MM-DD
  note: string | null;
  createdAt: string;
};

export type ExpenseInput = {
  amount: number;
  currency: Currency;
  category: Category;
  date: string;
  note: string | null;
};

export type CurrencyTotal = {
  currency: Currency;
  total: number;
  count: number;
  categories: { category: Category; total: number; count: number }[];
};

/** Итоги за месяц отдельно по каждой валюте (без конвертации), самая частая валюта первая. */
export type MonthlySummary = {
  year: number;
  month: number;
  currencies: CurrencyTotal[];
};

export type CurrentUser = { id: number; firstName: string; lastName: string | null; username: string | null };

export class ApiError extends Error {
  constructor(message: string, readonly status: number) {
    super(message);
  }
}

// Токен хранится только в памяти: при каждом открытии приложения вход выполняется заново
// по свежему initData, а в localStorage нечего украсть.
let accessToken: string | null = null;

export async function login(): Promise<CurrentUser> {
  const response = await fetch("/api/auth/telegram", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ initData: WebApp.initData }),
  });
  if (!response.ok) {
    throw new ApiError(
      response.status === 429 ? "Слишком много попыток входа. Подождите минуту." : "Не удалось войти. Закройте и снова откройте приложение.",
      response.status,
    );
  }
  const data = (await response.json()) as { accessToken: string; user: CurrentUser };
  accessToken = data.accessToken;
  return data.user;
}

async function request<T>(path: string, init: RequestInit = {}, retried = false): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      ...init.headers,
    },
  });

  // Токен истёк, пока приложение было открыто: один раз входим заново и повторяем запрос.
  if (response.status === 401 && !retried) {
    await login();
    return request<T>(path, init, true);
  }

  if (!response.ok) throw new ApiError(await readError(response), response.status);
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

async function readError(response: Response): Promise<string> {
  try {
    const problem = (await response.json()) as { errors?: Record<string, string[]>; title?: string };
    const first = problem.errors && Object.values(problem.errors)[0]?.[0];
    return first ?? problem.title ?? "Что-то пошло не так.";
  } catch {
    return response.status === 404 ? "Запись не найдена." : "Что-то пошло не так.";
  }
}

export const api = {
  listExpenses: (from: string, to: string) =>
    request<Expense[]>(`/api/expenses?${new URLSearchParams({ from, to })}`),
  getSummary: (year: number, month: number) =>
    request<MonthlySummary>(`/api/expenses/summary?${new URLSearchParams({ year: String(year), month: String(month) })}`),
  createExpense: (input: ExpenseInput) =>
    request<Expense>("/api/expenses", { method: "POST", body: JSON.stringify(input) }),
  updateExpense: (id: string, input: ExpenseInput) =>
    request<Expense>(`/api/expenses/${encodeURIComponent(id)}`, { method: "PUT", body: JSON.stringify(input) }),
  deleteExpense: (id: string) =>
    request<void>(`/api/expenses/${encodeURIComponent(id)}`, { method: "DELETE" }),
};
