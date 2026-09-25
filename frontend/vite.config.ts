import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    // В разработке запросы /api уходят на локальный бэкенд (dotnet run).
    proxy: { "/api": "http://localhost:5252" },
  },
});
