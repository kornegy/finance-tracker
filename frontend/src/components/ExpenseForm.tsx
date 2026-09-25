import { useEffect, useRef, useState } from "react";
import { api, type Expense } from "../api";
import { CATEGORIES, CATEGORY_INFO, isCategory, type Category } from "../categories";
import { parseAmount, todayIso } from "../format";
import { haptic } from "../telegram";

const LAST_CATEGORY_KEY = "lastCategory";

type Props = {
  /** Если передан — форма редактирует существующий расход. */
  expense?: Expense;
  onSaved: (expense: Expense) => void;
  onDelete?: () => void;
  onCancel?: () => void;
};

/**
 * Форма расхода. Быстрый путь: ввести сумму -> (выбрать категорию) -> "Сохранить".
 * Последняя категория запоминается, поэтому для частых трат хватает суммы и одного нажатия.
 */
export default function ExpenseForm({ expense, onSaved, onDelete, onCancel }: Props) {
  const [amount, setAmount] = useState(expense ? String(expense.amount).replace(".", ",") : "");
  const [category, setCategory] = useState<Category | null>(expense?.category ?? readLastCategory());
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
      const input = { amount: parsedAmount, category, date, note: note.trim() || null };
      const saved = expense ? await api.updateExpense(expense.id, input) : await api.createExpense(input);
      writeLastCategory(category);
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
        <input
          id="amount"
          ref={amountRef}
          inputMode="decimal"
          autoComplete="off"
          placeholder="0"
          value={amount}
          maxLength={14}
          onChange={(e) => setAmount(e.target.value.replace(/[^\d.,]/g, ""))}
          className="mt-1 w-full bg-transparent text-4xl font-semibold outline-none placeholder:text-tg-hint/50"
        />
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

function readLastCategory(): Category | null {
  try {
    const value = localStorage.getItem(LAST_CATEGORY_KEY);
    return isCategory(value) ? value : null;
  } catch {
    return null;
  }
}

function writeLastCategory(category: Category) {
  try {
    localStorage.setItem(LAST_CATEGORY_KEY, category);
  } catch {
    // Не критично: просто не запомним категорию.
  }
}
