import { describe, expect, it } from "vitest";
import indexHtml from "../../index.html?raw";
import sitemapXml from "../../public/sitemap.xml?raw";
import { DEFAULT_LOCALE, SUPPORTED_LOCALES } from "../i18n/locales";

const SITE_URL = "https://htwind.vercel.app";

function readDocument(markup: string): Document {
	return new DOMParser().parseFromString(markup, "text/xml");
}

function readHtmlDocument(markup: string): Document {
	return new DOMParser().parseFromString(markup, "text/html");
}

function expectedLocaleUrl(locale: string): string {
	return `${SITE_URL}/${locale}/`;
}

function parseSitemapEntries(document: Document) {
	return [...document.getElementsByTagName("url")].map((urlNode) => {
		const loc = urlNode.getElementsByTagName("loc")[0]?.textContent ?? "";
		const alternates = [...urlNode.getElementsByTagName("xhtml:link")].map(
			(link) => ({
				hreflang: link.getAttribute("hreflang") ?? "",
				href: link.getAttribute("href") ?? "",
			}),
		);
		return { loc, alternates };
	});
}

describe("sitemap locale contract", () => {
	const sitemapDoc = readDocument(sitemapXml);
	const entries = parseSitemapEntries(sitemapDoc);
	const expectedLocales = [...SUPPORTED_LOCALES];

	it("contains a root entry plus one entry per supported locale", () => {
		expect(entries).toHaveLength(expectedLocales.length + 1);

		const locs = entries.map((entry) => entry.loc);
		expect(locs).toContain(`${SITE_URL}/`);
		for (const locale of expectedLocales) {
			expect(locs).toContain(expectedLocaleUrl(locale));
		}
	});

	it.each([
		...expectedLocales,
	])("declares reciprocal hreflang alternates for %s", (locale) => {
		const entry = entries.find(
			(candidate) => candidate.loc === expectedLocaleUrl(locale),
		);
		expect(entry).toBeDefined();

		const hreflangs =
			entry?.alternates.map((alternate) => alternate.hreflang) ?? [];
		for (const supported of expectedLocales) {
			expect(hreflangs).toContain(supported);
			const alternate = entry?.alternates.find(
				(candidate) => candidate.hreflang === supported,
			);
			expect(alternate?.href).toBe(expectedLocaleUrl(supported));
		}
		expect(hreflangs).toContain("x-default");
	});

	it("points x-default at the bare root URL from every entry", () => {
		for (const entry of entries) {
			const xDefault = entry.alternates.find(
				(alternate) => alternate.hreflang === "x-default",
			);
			expect(xDefault?.href).toBe(`${SITE_URL}/`);
		}
	});

	it("keeps the x-default <url> entry free of a language path", () => {
		const rootEntry = entries.find((entry) => entry.loc === `${SITE_URL}/`);
		const selfDefault = rootEntry?.alternates.find(
			(alternate) =>
				alternate.hreflang === "x-default" && alternate.href === rootEntry.loc,
		);
		expect(selfDefault).toBeDefined();
	});
});

describe("index.html static SEO contract", () => {
	const indexDoc = readHtmlDocument(indexHtml);

	it("declares static hreflang links for every supported locale plus x-default", () => {
		const links = [
			...indexDoc.querySelectorAll('link[rel="alternate"][hreflang]'),
		];
		const byHreflang = new Map(
			links.map((link) => [
				link.getAttribute("hreflang"),
				link.getAttribute("href"),
			]),
		);

		for (const locale of SUPPORTED_LOCALES) {
			expect(byHreflang.get(locale)).toBe(expectedLocaleUrl(locale));
		}
		expect(byHreflang.get("x-default")).toBe(`${SITE_URL}/`);
		expect(links).toHaveLength(SUPPORTED_LOCALES.length + 1);
	});

	it("lists every supported locale in the noscript language menu", () => {
		const noscript = indexDoc.querySelector("noscript");
		expect(noscript).not.toBeNull();

		const markup = noscript?.innerHTML ?? "";
		const noscriptDocument = new DOMParser().parseFromString(
			markup,
			"text/html",
		);
		const anchors = [...noscriptDocument.querySelectorAll("a[hreflang]")];

		const hreflangs = anchors.map((anchor) => anchor.getAttribute("hreflang"));
		for (const locale of SUPPORTED_LOCALES) {
			expect(hreflangs).toContain(locale);
		}
		expect(hreflangs).toHaveLength(SUPPORTED_LOCALES.length);

		for (const anchor of anchors) {
			const locale = anchor.getAttribute("hreflang") ?? "";
			expect(anchor.getAttribute("href")).toBe(`/${locale}/`);
			expect(anchor.textContent?.trim().length ?? 0).toBeGreaterThan(0);
		}
	});

	it("uses the default locale for the static canonical URL", () => {
		const canonical = indexDoc.querySelector('link[rel="canonical"]');
		expect(canonical?.getAttribute("href")).toBe(
			expectedLocaleUrl(DEFAULT_LOCALE),
		);
	});
});
