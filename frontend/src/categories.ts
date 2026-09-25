// Должны совпадать с enum ExpenseCategory на бэкенде (backend/.../Models/ExpenseCategory.cs).
export const CATEGORIES = [
  "Food",
  "Transport",
  "Housing",
  "Utilities",
  "Health",
  "Entertainment",
  "Shopping",
  "Education",
  "Other",
] as const;

export type Category = (typeof CATEGORIES)[number];

export const CATEGORY_INFO: Record<Category, { label: string; emoji: string }> = {
  Food: { label: "Еда", emoji: "🍔" },
  Transport: { label: "Транспорт", emoji: "🚕" },
  Housing: { label: "Жильё", emoji: "🏠" },
  Utilities: { label: "Счета", emoji: "💡" },
  Health: { label: "Здоровье", emoji: "💊" },
  Entertainment: { label: "Досуг", emoji: "🎬" },
  Shopping: { label: "Покупки", emoji: "🛍️" },
  Education: { label: "Учёба", emoji: "📚" },
  Other: { label: "Другое", emoji: "📦" },
};

export function isCategory(value: unknown): value is Category {
  return typeof value === "string" && (CATEGORIES as readonly string[]).includes(value);
}
