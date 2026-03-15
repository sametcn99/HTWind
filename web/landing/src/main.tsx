import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import App from "./App.tsx";
import "./i18n/setup";
import { DEFAULT_LOCALE } from "./i18n/locales";
import { getLocaleMessages } from "./i18n/resources";

const rootElement = document.getElementById("root");

if (!rootElement) {
	throw new Error(getLocaleMessages(DEFAULT_LOCALE).errors.missingRoot);
}

createRoot(rootElement).render(
	<StrictMode>
		<App />
	</StrictMode>,
);
