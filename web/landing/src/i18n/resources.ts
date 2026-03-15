import arMessages from "../locales/ar.json";
import deMessages from "../locales/de.json";
import enMessages from "../locales/en.json";
import esMessages from "../locales/es.json";
import frMessages from "../locales/fr.json";
import hiMessages from "../locales/hi.json";
import idMessages from "../locales/id.json";
import itMessages from "../locales/it.json";
import jaMessages from "../locales/ja.json";
import koMessages from "../locales/ko.json";
import nlMessages from "../locales/nl.json";
import plMessages from "../locales/pl.json";
import ptMessages from "../locales/pt.json";
import ruMessages from "../locales/ru.json";
import trMessages from "../locales/tr.json";
import ukMessages from "../locales/uk.json";
import viMessages from "../locales/vi.json";
import zhMessages from "../locales/zh.json";
import type { LocaleCode } from "./locales";
import type { LocaleMessages } from "./types";

type TranslationResource = {
	translation: LocaleMessages;
};

export const resources: Record<LocaleCode, TranslationResource> = {
	ar: { translation: arMessages },
	de: { translation: deMessages },
	en: { translation: enMessages },
	es: { translation: esMessages },
	fr: { translation: frMessages },
	hi: { translation: hiMessages },
	id: { translation: idMessages },
	it: { translation: itMessages },
	ja: { translation: jaMessages },
	ko: { translation: koMessages },
	nl: { translation: nlMessages },
	pl: { translation: plMessages },
	pt: { translation: ptMessages },
	ru: { translation: ruMessages },
	tr: { translation: trMessages },
	uk: { translation: ukMessages },
	vi: { translation: viMessages },
	zh: { translation: zhMessages },
};

export function getLocaleMessages(locale: LocaleCode): LocaleMessages {
	return resources[locale].translation;
}
