import i18n from "i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import { initReactI18next } from "react-i18next";
import {
	DEFAULT_LOCALE,
	LOCALE_STORAGE_KEY,
	SUPPORTED_LOCALES,
} from "./locales";
import { resources } from "./resources";

if (!i18n.isInitialized) {
	void i18n
		.use(LanguageDetector)
		.use(initReactI18next)
		.init({
			resources,
			defaultNS: "translation",
			ns: ["translation"],
			fallbackLng: DEFAULT_LOCALE,
			supportedLngs: [...SUPPORTED_LOCALES],
			load: "languageOnly",
			nonExplicitSupportedLngs: true,
			cleanCode: true,
			returnObjects: true,
			interpolation: {
				escapeValue: false,
			},
			detection: {
				order: ["path", "localStorage", "navigator"],
				lookupFromPathIndex: 0,
				lookupLocalStorage: LOCALE_STORAGE_KEY,
				caches: ["localStorage"],
			},
		});
}

export default i18n;
