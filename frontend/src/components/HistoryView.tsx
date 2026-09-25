import { useCallback, useEffect, useMemo, useState } from "react";
import { api, type Expense, type MonthlySummary } from "../api";
import { CATEGORY_INFO } from "../categories";
import { formatDay, formatMoney, formatMonth, lastDayOfMonth, toIsoDate } from "../format";
import { confirmAction, haptic } from "../telegram";
import ExpenseForm from "./ExpenseForm";

type Month = { year: number; month: number };

const currentMonth = (): Month => {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
};

/** Вкладка "История и статистика": сводка за месяц по категориям и список расходов. */
export default function HistoryView() {
  const [month, setMonth] = useState<Month>(currentMonth);
  const [summary, setSummary] = useState<MonthlySummary | null>(null);
  const [expenses, setExpenses] = useState<Expense[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState<Expense | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const from = toIsoDate(month.year, month.month, 1);
      const to = toIsoDate(month.year, month.month, lastDayOfMonth(month.year, month.month));
      const [s, list] = await Promise.all([api.getSummary(month.year, month.month), api.listExpenses(from, to)]);
      setSummary(s);
      setExpenses(list);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setLoading(false);
    }
  }, [month]);

  useEffect(() => {
    void load();
  }, [load]);

  const byDay = useMemo(() => {
    const groups = new Map<string, Expense[]>();
    for (const e of expenses) groups.set(e.date, [...(groups.get(e.date) ?? []), e]);
    return [...groups.entries()];
  }, [expenses]);

  const isCurrentMonth = month.year === currentMonth().year && month.month === currentMonth().month;

  function shiftMonth(delta: number) {
    haptic("select");
    setMonth(({ year, month: m }) => {
      const d = new Date(year, m - 1 + delta, 1);
      return { year: d.getFullYear(), month: d.getMonth() + 1 };
    });
  }

  async function remove(expense: Expense) {
    if (!(await confirmAction("Удалить этот расход?"))) return;
    try {
      await api.deleteExpense(expense.id);
      haptic("success");
      setEditing(null);
      await load();
    } catch (e) {
      haptic("error");
      setError((e as Error).message);
    }
  }

  if (editing) {
    return (
      <ExpenseForm
        key={editing.id}
        expense={editing}
        onSaved={() => {
          setEditing(null);
          void load();
        }}
        onDelete={() => void remove(editing)}
        onCancel={() => setEditing(null)}
      />
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <header className="flex items-center justify-between">
        <button onClick={() => shiftMonth(-1)} className="h-10 w-10 rounded-full bg-tg-section-bg text-xl" aria-label="Предыдущий месяц">
          ‹
        </button>
        <h2 className="text-lg font-semibold">{formatMonth(month.year, month.month)}</h2>
        <button
          onClick={() => shiftMonth(1)}
          disabled={isCurrentMonth}
          className="h-10 w-10 rounded-full bg-tg-section-bg text-xl disabled:opacity-30"
          aria-label="Следующий месяц"
        >
          ›
        </button>
      </header>

      {error && <p className="text-center text-sm text-tg-destructive">{error}</p>}

      {/* Отдельная карточка на каждую валюту: суммы в разных валютах не складываем. */}
      {summary?.currencies.map((cur) => (
        <section key={cur.currency} className="rounded-2xl bg-tg-section-bg p-4">
          <p className="text-sm text-tg-hint">Потрачено за месяц</p>
          <p className="mt-1 text-3xl font-semibold">{formatMoney(cur.total, cur.currency)}</p>

          <ul className="mt-4 flex flex-col gap-3">
            {cur.categories.map((c) => {
              const share = cur.total > 0 ? (c.total / cur.total) * 100 : 0;
              return (
                <li key={c.category}>
                  <div className="flex justify-between text-sm">
                    <span>
                      {CATEGORY_INFO[c.category].emoji} {CATEGORY_INFO[c.category].label}
                    </span>
                    <span className="font-medium">{formatMoney(c.total, cur.currency)}</span>
                  </div>
                  <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-tg-secondary-bg">
                    <div className="h-full rounded-full bg-tg-button" style={{ width: `${Math.max(share, 2)}%` }} />
                  </div>
                </li>
              );
            })}
          </ul>
        </section>
      ))}

      {loading && !summary && <p className="py-8 text-center text-tg-hint">Загрузка…</p>}

      {!loading && expenses.length === 0 && !error && (
        <p className="py-8 text-center text-tg-hint">В этом месяце расходов нет</p>
      )}

      {byDay.map(([day, items]) => (
        <section key={day}>
          <h3 className="mb-1.5 px-1 text-sm text-tg-hint">{formatDay(day)}</h3>
          <ul className="divide-y divide-tg-separator overflow-hidden rounded-2xl bg-tg-section-bg">
            {items.map((e) => (
              <li key={e.id}>
                <button onClick={() => setEditing(e)} className="flex w-full items-center gap-3 px-4 py-3 text-left">
                  <span className="text-2xl">{CATEGORY_INFO[e.category].emoji}</span>
                  <span className="min-w-0 flex-1">
                    <span className="block">{CATEGORY_INFO[e.category].label}</span>
                    {e.note && <span className="block truncate text-sm text-tg-hint">{e.note}</span>}
                  </span>
                  <span className="font-medium">{formatMoney(e.amount, e.currency)}</span>
                </button>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  );
}
