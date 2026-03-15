export const DEFAULT_LOCALE = "en";
export const LOCALE_STORAGE_KEY = "htwind:locale";
export const SUPPORTED_LOCALES = [
	"en",
	"tr",
	"hi",
	"zh",
	"de",
	"fr",
	"ru",
	"ar",
	"ja",
	"es",
	"pt",
	"it",
	"ko",
	"id",
	"vi",
	"nl",
	"pl",
	"uk",
] as const;

export type LocaleCode = (typeof SUPPORTED_LOCALES)[number];

export const LOCALE_SEGMENTS: Record<LocaleCode, string> = {
	en: "en",
	tr: "tr",
	hi: "hi",
	zh: "zh",
	de: "de",
	fr: "fr",
	ru: "ru",
	ar: "ar",
	ja: "ja",
	es: "es",
	pt: "pt",
	it: "it",
	ko: "ko",
	id: "id",
	vi: "vi",
	nl: "nl",
	pl: "pl",
	uk: "uk",
};

export const INTL_LOCALE_MAP: Record<LocaleCode, string> = {
	en: "en-US",
	tr: "tr-TR",
	hi: "hi-IN",
	zh: "zh-CN",
	de: "de-DE",
	fr: "fr-FR",
	ru: "ru-RU",
	ar: "ar-SA",
	ja: "ja-JP",
	es: "es-ES",
	pt: "pt-BR",
	it: "it-IT",
	ko: "ko-KR",
	id: "id-ID",
	vi: "vi-VN",
	nl: "nl-NL",
	pl: "pl-PL",
	uk: "uk-UA",
};

export function isLocaleCode(
	value: string | null | undefined,
): value is LocaleCode {
	return (
		value !== undefined &&
		value !== null &&
		SUPPORTED_LOCALES.includes(value as LocaleCode)
	);
}

export function normalizeLocale(
	value: string | null | undefined,
): LocaleCode | null {
	if (!value) {
		return null;
	}

	const normalized = value.trim().toLowerCase().replaceAll("_", "-");
	const base = normalized.split("-")[0];

	if (base === "zh") {
		return "zh";
	}

	if (isLocaleCode(base)) {
		return base;
	}

	return null;
}

export function detectBrowserLocale(
	languages: readonly string[] | undefined,
): LocaleCode {
	if (!languages?.length) {
		return DEFAULT_LOCALE;
	}

	for (const language of languages) {
		const locale = normalizeLocale(language);
		if (locale) {
			return locale;
		}
	}

	return DEFAULT_LOCALE;
}

export function getLocaleFromPath(pathname: string): LocaleCode | null {
	const segments = pathname.split("/").filter(Boolean);
	if (segments.length === 0) {
		return null;
	}

	const locale = normalizeLocale(segments[0]);
	return locale ? locale : null;
}

export function stripLocalePrefix(pathname: string): string {
	const locale = getLocaleFromPath(pathname);
	if (!locale) {
		return pathname || "/";
	}

	const prefix = `/${LOCALE_SEGMENTS[locale]}`;
	const strippedPath = pathname.slice(prefix.length);

	if (!strippedPath || strippedPath === "/") {
		return "/";
	}

	return strippedPath.startsWith("/") ? strippedPath : `/${strippedPath}`;
}

export function getLocalePath(locale: LocaleCode): string {
	return `/${LOCALE_SEGMENTS[locale]}/`;
}

export function buildLocalizedPath(
	locale: LocaleCode,
	pathname: string,
	search = "",
	hash = "",
): string {
	const basePath = stripLocalePrefix(pathname);

	const normalizedBase = basePath === "/" ? "" : basePath.replace(/\/$/, "");
	return `${`/${locale}${normalizedBase}/`.replace(/\/+/g, "/")}${search}${hash}`;
}

export function resolveInitialLocale(options: {
	pathname: string;
	savedLocale?: string | null;
	browserLanguages?: readonly string[];
}): LocaleCode {
	const pathLocale = getLocaleFromPath(options.pathname);
	if (pathLocale) {
		return pathLocale;
	}

	const savedLocale = normalizeLocale(options.savedLocale);
	if (savedLocale) {
		return savedLocale;
	}

	return detectBrowserLocale(options.browserLanguages);
}

export function getAutoRedirectPath(options: {
	pathname: string;
	search: string;
	hash: string;
	savedLocale?: string | null;
	browserLanguages?: readonly string[];
}): string | null {
	const explicitLocale = getLocaleFromPath(options.pathname);
	if (explicitLocale) {
		return null;
	}

	const preferredLocale = resolveInitialLocale(options);

	return buildLocalizedPath(
		preferredLocale,
		options.pathname,
		options.search,
		options.hash,
	);
}
