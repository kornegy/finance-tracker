import WebApp from "./webapp";

// Небольшие обёртки над Telegram API с запасным вариантом для старых клиентов и обычного браузера.

export function haptic(type: "success" | "error" | "select") {
  try {
    if (!WebApp.isVersionAtLeast("6.1")) return;
    if (type === "select") WebApp.HapticFeedback.selectionChanged();
    else WebApp.HapticFeedback.notificationOccurred(type);
  } catch {
    // Вибрация необязательна.
  }
}

export function confirmAction(message: string): Promise<boolean> {
  if (WebApp.initData && WebApp.isVersionAtLeast("6.2")) {
    return new Promise((resolve) => WebApp.showConfirm(message, (ok) => resolve(ok)));
  }
  return Promise.resolve(window.confirm(message));
}
