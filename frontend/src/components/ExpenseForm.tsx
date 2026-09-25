import { useEffect, useRef, useState } from "react";
import { api, type Expense } from "../api";
import { CATEGORIES, CATEGORY_INFO, isCategory, type Category } from "../categories";
import { CURRENCIES, CURRENCY_INFO, isCurrency, type Currency } from "../currencies";
import { parseAmount, todayIso } from "../format";
import { haptic } from "../telegram";

const LAST_CATEGORY_KEY = "lastCategory";
const LAST_CURRENCY_KEY = "lastCurrency";

type Props = {
  /** Если передан — форма редактирует существующий расход. */
  expense?: Expense;
  onSaved: (expense: Expense) => void;
  onDelete?: () => void;
  onCancel?: () => void;
};

/**
 * Форма расхода. Быстрый путь: ввести сумму -> (выбрать категорию) -> "Сохранить".
 * Последние категория и валюта запоминаются, поэтому для частых трат хватает суммы и одного нажатия.
 */
export default function ExpenseForm({ expense, onSaved, onDelete, onCancel }: Props) {
  const [amount, setAmount] = useState(expense ? String(expense.amount).replace(".", ",") : "");
  const [category, setCategory] = useState<Category | null>(expense?.category ?? readStored(LAST_CATEGORY_KEY, isCategory));
  const [currency, setCurrency] = useState<Currency>(expense?.currency ?? readStored(LAST_CURRENCY_KEY, isCurrency) ?? CURRENCIES[0]);
  const [date, setDate] = useState(expense?.date ?? todayIso());
  const [note, setNote] = useState(expense?.note ?? "");
  const [showDetails, setShowDetails] = useState(Boolean(expense));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savedFlash, setSavedFlash] = useState(false);
  const amountRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (!expense) amountRef.current?.focus();
  }, [expense]);

  const parsedAmount = parseAmount(amount);
  const canSave = parsedAmount !== null && category !== null && !saving;

  async function save() {
    if (!canSave) return;
    setSaving(true);
    setError(null);
    try {
      const input = { amount: parsedAmount, currency, category, date, note: note.trim() || null };
      const saved = expense ? await api.updateExpense(expense.id, input) : await api.createExpense(input);
      writeStored(LAST_CATEGORY_KEY, category);
      writeStored(LAST_CURRENCY_KEY, currency);
      haptic("success");
      if (!expense) {
        setAmount("");
        setNote("");
        setDate(todayIso());
        setShowDetails(false);
        setSavedFlash(true);
        setTimeout(() => setSavedFlash(false), 1500);
        amountRef.current?.focus();
      }
      onSaved(saved);
    } catch (e) {
      haptic("error");
      setError((e as Error).message);
    } finally {
      setSaving(false);
    }
  }

  return (
    <form
      className="flex flex-col gap-4"
      onSubmit={(e) => {
        e.preventDefault();
        void save();
      }}
    >
      <section className="rounded-2xl bg-tg-section-bg p-4">
        <label className="text-sm text-tg-hint" htmlFor="amount">
          Сумма
        </label>
        <div className="mt-1 flex items-center gap-3">
          <input
            id="amount"
            ref={amountRef}
            inputMode="decimal"
            autoComplete="off"
            placeholder="0"
            value={amount}
            maxLength={14}
            onChange={(e) => setAmount(e.target.value.replace(/[^\d.,]/g, ""))}
            className="min-w-0 flex-1 bg-transparent text-4xl font-semibold outline-none placeholder:text-tg-hint/50"
          />
          {/* Нативный select поверх "пилюли": на телефоне открывается системный список выбора. */}
          <label className="relative flex shrink-0 items-center gap-1 rounded-xl bg-tg-secondary-bg px-3 py-2 text-lg font-semibold text-tg-accent">
            {CURRENCY_INFO[currency].symbol}
            <span className="text-xs text-tg-hint">▼</span>
            <select
              aria-label="Валюта"
              value={currency}
              onChange={(e) => {
                haptic("select");
                if (isCurrency(e.target.value)) setCurrency(e.target.value);
              }}
              className="absolute inset-0 cursor-pointer opacity-0"
            >
              {CURRENCIES.map((c) => (
                <option key={c} value={c}>
                  {CURRENCY_INFO[c].symbol} {c}, {CURRENCY_INFO[c].name}
                </option>
              ))}
            </select>
          </label>
        </div>
      </section>

      <section className="grid grid-cols-3 gap-2">
        {CATEGORIES.map((c) => (
          <button
            key={c}
            type="button"
            onClick={() => {
              haptic("select");
              setCategory(c);
            }}
            className={`flex flex-col items-center gap-1 rounded-2xl py-3 text-sm transition-colors ${
              category === c ? "bg-tg-button text-tg-button-text" : "bg-tg-section-bg text-tg-text"
            }`}
          >
            <span className="text-2xl leading-none">{CATEGORY_INFO[c].emoji}</span>
            {CATEGORY_INFO[c].label}
          </button>
        ))}
      </section>

      {showDetails ? (
        <section className="flex flex-col divide-y divide-tg-separator rounded-2xl bg-tg-section-bg">
          <label className="flex items-center justify-between px-4 py-3">
            <span>Дата</span>
            <input
              type="date"
              value={date}
              max={todayIso()}
              required
              onChange={(e) => setDate(e.target.value)}
              className="bg-transparent text-right text-tg-accent outline-none"
            />
          </label>
          <input
            placeholder="Заметка (необязательно)"
            value={note}
            maxLength={500}
            onChange={(e) => setNote(e.target.value)}
            className="bg-transparent px-4 py-3 outline-none placeholder:text-tg-hint"
          />
        </section>
      ) : (
        <button type="button" onClick={() => setShowDetails(true)} className="self-start px-1 text-sm text-tg-link">
          + Дата и заметка
        </button>
      )}

      {error && <p className="px-1 text-sm text-tg-destructive">{error}</p>}

      <button
        type="submit"
        disabled={!canSave}
        className="rounded-2xl bg-tg-button py-3.5 text-lg font-semibold text-tg-button-text transition-opacity disabled:opacity-40"
      >
        {saving ? "Сохраняю…" : savedFlash ? "✓ Сохранено" : expense ? "Сохранить изменения" : "Сохранить"}
      </button>

      {(onCancel || onDelete) && (
        <div className="flex justify-between px-1">
          {onCancel && (
            <button type="button" onClick={onCancel} className="text-tg-link">
              Отмена
            </button>
          )}
          {onDelete && (
            <button type="button" onClick={onDelete} className="text-tg-destructive">
              Удалить
            </button>
          )}
        </div>
      )}
    </form>
  );
}

function readStored<T extends string>(key: string, isValid: (value: unknown) => value is T): T | null {
  try {
    const value = localStorage.getItem(key);
    return isValid(value) ? value : null;
  } catch {
    return null;
  }
}

function writeStored(key: string, value: string) {
  try {
    localStorage.setItem(key, value);
  } catch {
    // Не критично: просто не запомним выбор.
  }
}
