import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import WebApp from "./webapp";
import App from "./App";
import AuthGate from "./components/AuthGate";
import "./index.css";

// Сообщаем Telegram, что приложение готово, и разворачиваем его на всю высоту.
WebApp.ready();
WebApp.expand();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AuthGate>
      <App />
    </AuthGate>
  </StrictMode>,
);
