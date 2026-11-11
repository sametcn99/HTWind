import { cleanup, renderHook } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { type LocaleCode, SUPPORTED_LOCALES } from "../i18n/locales";
import { getLocaleMessages } from "../i18n/resources";
import type { LocaleMessages } from "../i18n/types";
import { useHeadMetadata } from "./useHeadMetadata";

const SITE_URL = "https://htwind.vercel.app";
const MANAGED_HREFLANG_SELECTOR =
	'link[rel="alternate"][data-managed-hreflang="true"]';

function getJsonLd(id: string): Record<string, unknown> | null {
	const script = document.getElementById(id);
	if (!script) {
		return null;
	}

	return JSON.parse(script.textContent ?? "null") as Record<string, unknown>;
}

afterEach(() => {
	cleanup();
	document.head.innerHTML = "";
	document.title = "";
	delete document.documentElement.dataset.dir;
	document.documentElement.removeAttribute("lang");
	document.documentElement.removeAttribute("dir");
});

describe("useHeadMetadata", () => {
	it("sets document language, direction, and title for en", () => {
		const messages = getLocaleMessages("en");

		renderHook(() => useHeadMetadata("en", messages));

		expect(document.documentElement.lang).toBe("en");
		expect(document.documentElement.dir).toBe("ltr");
		expect(document.title).toBe(messages.seo.title);
	});

	it("sets dir=rtl and lang=ar for the Arabic locale", () => {
		const messages = getLocaleMessages("ar");

		renderHook(() => useHeadMetadata("ar", messages));

		expect(document.documentElement.lang).toBe("ar");
		expect(document.documentElement.dir).toBe("rtl");
	});

	it("renders the canonical URL for the active locale", () => {
		renderHook(() => useHeadMetadata("tr", getLocaleMessages("tr")));

		const canonical = document.head.querySelector('link[rel="canonical"]');
		expect(canonical?.getAttribute("href")).toBe(`${SITE_URL}/tr/`);
	});

	it("injects managed hreflang alternates for every locale plus x-default", () => {
		renderHook(() => useHeadMetadata("en", getLocaleMessages("en")));

		const links = [
			...document.head.querySelectorAll(MANAGED_HREFLANG_SELECTOR),
		];
		expect(links).toHaveLength(SUPPORTED_LOCALES.length + 1);

		const byHreflang = new Map(
			links.map((link) => [
				link.getAttribute("hreflang"),
				link.getAttribute("href"),
			]),
		);
		for (const locale of SUPPORTED_LOCALES) {
			expect(byHreflang.get(locale)).toBe(`${SITE_URL}/${locale}/`);
		}
		expect(byHreflang.get("x-default")).toBe(`${SITE_URL}/`);
	});

	it("injects the SEO meta tags from the active messages", () => {
		const messages = getLocaleMessages("en");

		renderHook(() => useHeadMetadata("en", messages));

		const head = document.head;
		expect(
			head.querySelector<HTMLMetaElement>('meta[name="description"]')?.content,
		).toBe(messages.seo.description);
		expect(
			head.querySelector<HTMLMetaElement>('meta[property="og:title"]')?.content,
		).toBe(messages.seo.openGraphTitle);
		expect(
			head.querySelector<HTMLMetaElement>('meta[property="og:description"]')
				?.content,
		).toBe(messages.seo.openGraphDescription);
		expect(
			head.querySelector<HTMLMetaElement>('meta[property="og:locale"]')
				?.content,
		).toBe(messages.meta.ogLocale);
		expect(
			head.querySelector<HTMLMetaElement>('meta[name="twitter:title"]')
				?.content,
		).toBe(messages.seo.twitterTitle);
		expect(
			head.querySelector<HTMLMetaElement>('meta[property="og:url"]')?.content,
		).toBe(`${SITE_URL}/en/`);
		expect(
			head.querySelectorAll('meta[data-og-locale-alternate="true"]'),
		).toHaveLength(SUPPORTED_LOCALES.length - 1);
		const alternates = [
			...head.querySelectorAll('meta[data-og-locale-alternate="true"]'),
		].map((node) => node.getAttribute("property"));
		expect(alternates).toContain("tr_TR");
		expect(alternates).not.toContain(messages.meta.ogLocale);
	});

	it("injects exactly four JSON-LD structured data scripts", () => {
		const messages = getLocaleMessages("en");

		renderHook(() => useHeadMetadata("en", messages));

		const software = getJsonLd("seo-structured-data-software");
		const website = getJsonLd("seo-structured-data-website");
		const organization = getJsonLd("seo-structured-data-organization");
		const faq = getJsonLd("seo-structured-data-faq");

		expect(software?.["@type"]).toBe("SoftwareApplication");
		expect(website?.["@type"]).toBe("WebSite");
		expect(organization?.["@type"]).toBe("Organization");
		expect(faq?.["@type"]).toBe("FAQPage");

		expect(software?.inLanguage).toBe("en");
		const mainEntity = faq?.mainEntity as Array<Record<string, unknown>>;
		expect(mainEntity).toHaveLength(messages.content.faqItems.length);
		expect(mainEntity[0]?.name).toBe(messages.content.faqItems[0].question);
	});

	it("updates state without duplicating managed nodes on re-render", () => {
		const en = getLocaleMessages("en");
		const { rerender } = renderHook(
			({
				locale,
				messages,
			}: {
				locale: LocaleCode;
				messages: LocaleMessages;
			}) => useHeadMetadata(locale, messages),
			{ initialProps: { locale: "en" as LocaleCode, messages: en } },
		);

		rerender({ locale: "de", messages: getLocaleMessages("de") });

		expect(document.documentElement.lang).toBe("de");
		expect(
			document.head.querySelectorAll(MANAGED_HREFLANG_SELECTOR),
		).toHaveLength(SUPPORTED_LOCALES.length + 1);
		expect(
			document.head.querySelectorAll('meta[data-og-locale-alternate="true"]'),
		).toHaveLength(SUPPORTED_LOCALES.length - 1);
		expect(
			document.head.querySelectorAll('link[rel="canonical"]'),
		).toHaveLength(1);
		expect(
			document.head.querySelectorAll('meta[property="og:locale"]'),
		).toHaveLength(1);
	});
});
