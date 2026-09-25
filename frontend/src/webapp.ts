import sdk from "@twa-dev/sdk";

// @twa-dev/sdk опубликован как CommonJS. Сборщики по-разному отдают его default-экспорт
// (иногда объект WebApp, иногда { default: WebApp }), поэтому нормализуем здесь один раз.
// Остальной код импортирует WebApp только из этого файла.
const WebApp: typeof sdk = (sdk as unknown as { default?: typeof sdk }).default ?? sdk;

export default WebApp;
