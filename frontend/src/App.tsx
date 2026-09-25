import { useState } from "react";
import ExpenseForm from "./components/ExpenseForm";
import HistoryView from "./components/HistoryView";
import { haptic } from "./telegram";

type Tab = "add" | "history";

const TABS: { id: Tab; label: string; icon: string }[] = [
  { id: "add", label: "Новый расход", icon: "＋" },
  { id: "history", label: "История", icon: "☰" },
];

export default function App() {
  const [tab, setTab] = useState<Tab>("add");

  return (
    <div className="mx-auto flex min-h-screen max-w-md flex-col">
      <main className="flex-1 p-4 pb-24">{tab === "add" ? <ExpenseForm onSaved={() => {}} /> : <HistoryView />}</main>

      <nav className="fixed inset-x-0 bottom-0 border-t border-tg-separator bg-tg-bg pb-[env(safe-area-inset-bottom)]">
        <div className="mx-auto flex max-w-md">
          {TABS.map((t) => (
            <button
              key={t.id}
              onClick={() => {
                haptic("select");
                setTab(t.id);
              }}
              className={`flex flex-1 flex-col items-center gap-0.5 py-2.5 text-xs ${tab === t.id ? "text-tg-button" : "text-tg-hint"}`}
            >
              <span className="text-lg leading-none">{t.icon}</span>
              {t.label}
            </button>
          ))}
        </div>
      </nav>
    </div>
  );
}
