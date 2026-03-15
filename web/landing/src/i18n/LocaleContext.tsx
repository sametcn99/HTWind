import { createContext, type ReactNode, useContext, useMemo } from "react";
import { I18nextProvider, useTranslation } from "react-i18next";
import type { LocaleCode } from "./locales";
import { getLocaleMessages } from "./resources";
import i18n from "./setup";
import type { LocaleMessages } from "./types";

type LocaleContextValue = {
	locale: LocaleCode;
	messages: LocaleMessages;
	changeLocale: (locale: LocaleCode) => void;
};

const LocaleContext = createContext<LocaleContextValue | null>(null);

type LocaleProviderProps = {
	children: ReactNode;
	locale: LocaleCode;
	changeLocale: (locale: LocaleCode) => void;
};

export function LocaleProvider({
	children,
	locale,
	changeLocale,
}: LocaleProviderProps) {
	return (
		<I18nextProvider i18n={i18n}>
			<LocaleContextBridge locale={locale} changeLocale={changeLocale}>
				{children}
			</LocaleContextBridge>
		</I18nextProvider>
	);
}

type LocaleContextBridgeProps = LocaleProviderProps;

function LocaleContextBridge({
	children,
	locale,
	changeLocale,
}: LocaleContextBridgeProps) {
	const { i18n: translationI18n } = useTranslation();
	const messages = useMemo(() => {
		const bundle = translationI18n.getResourceBundle(locale, "translation") as
			| LocaleMessages
			| undefined;

		return bundle ?? getLocaleMessages(locale);
	}, [locale, translationI18n]);

	return (
		<LocaleContext.Provider
			value={{
				locale,
				messages,
				changeLocale,
			}}
		>
			{children}
		</LocaleContext.Provider>
	);
}

// This hook is intentionally colocated with the provider to keep the locale facade stable during the i18next migration.
// eslint-disable-next-line react-refresh/only-export-components
export function useLocale() {
	const context = useContext(LocaleContext);

	if (!context) {
		throw new Error("useLocale must be used inside LocaleProvider.");
	}

	return context;
}
