import { useCallback, useEffect, useState, type ReactNode } from "react";
import WebApp from "../webapp";
import { login } from "../api";

type State = { status: "loading" } | { status: "ready" } | { status: "error"; message: string };

/**
 * Обёртка авторизации: обменивает initData от Telegram на JWT и только после этого
 * показывает приложение. Вне Telegram initData пустая, поэтому показываем подсказку.
 */
export default function AuthGate({ children }: { children: ReactNode }) {
  const [state, setState] = useState<State>({ status: "loading" });

  const signIn = useCallback(() => {
    setState({ status: "loading" });
    login()
      .then(() => setState({ status: "ready" }))
      .catch((e: Error) => setState({ status: "error", message: e.message }));
  }, []);

  useEffect(() => {
    if (WebApp.initData) signIn();
  }, [signIn]);

  if (!WebApp.initData) {
    return (
      <Centered>
        <div className="text-5xl">📱</div>
        <p className="text-lg font-semibold">Откройте приложение в Telegram</p>
        <p className="text-tg-hint">Оно работает только как Mini App внутри Telegram.</p>
      </Centered>
    );
  }

  if (state.status === "loading") {
    return (
      <Centered>
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-tg-button border-t-transparent" />
      </Centered>
    );
  }

  if (state.status === "error") {
    return (
      <Centered>
        <p className="text-tg-destructive">{state.message}</p>
        <button onClick={signIn} className="rounded-xl bg-tg-button px-5 py-2.5 font-medium text-tg-button-text">
          Повторить
        </button>
      </Centered>
    );
  }

  return <>{children}</>;
}

function Centered({ children }: { children: ReactNode }) {
  return <div className="flex min-h-screen flex-col items-center justify-center gap-3 p-6 text-center">{children}</div>;
}
