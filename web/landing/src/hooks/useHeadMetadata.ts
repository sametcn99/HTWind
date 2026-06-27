import { useEffect } from "react";
import { GITHUB_REPOSITORY_URL, RELEASES_URL } from "../config/constants";
import {
	getLocalePath,
	INTL_LOCALE_MAP,
	type LocaleCode,
	SUPPORTED_LOCALES,
} from "../i18n/locales";
import type { LocaleMessages } from "../i18n/types";

const SITE_URL = "https://htwind.vercel.app";
const OG_IMAGE_URL = `${SITE_URL}/og-image.png`;
const DEFAULT_CANONICAL_URL = `${SITE_URL}/`;
const RTL_LOCALES: ReadonlySet<string> = new Set(["ar", "he", "fa", "ur"]);

function isRtlLocale(locale: LocaleCode): boolean {
	return RTL_LOCALES.has(locale);
}

function getAbsoluteUrl(locale: LocaleCode): string {
	const path = getLocalePath(locale);
	return new URL(path, SITE_URL).toString();
}

function upsertMeta(selector: string, attributes: Record<string, string>) {
	let node = document.head.querySelector<HTMLMetaElement>(selector);

	if (!node) {
		node = document.createElement("meta");
		document.head.append(node);
	}

	for (const [key, value] of Object.entries(attributes)) {
		node.setAttribute(key, value);
	}
}

function upsertMetaArray(
	baseSelector: string,
	key: string,
	values: string[],
	dataFlag: string,
) {
	document.head
		.querySelectorAll(`meta[${baseSelector}][data-${dataFlag}="true"]`)
		.forEach((node) => node.remove());

	for (const value of values) {
		const node = document.createElement("meta");
		node.setAttribute(key, value);
		node.setAttribute(`data-${dataFlag}`, "true");
		document.head.append(node);
	}
}

function upsertLink(selector: string, attributes: Record<string, string>) {
	let node = document.head.querySelector<HTMLLinkElement>(selector);

	if (!node) {
		node = document.createElement("link");
		document.head.append(node);
	}

	for (const [key, value] of Object.entries(attributes)) {
		node.setAttribute(key, value);
	}
}

function upsertJsonLd(id: string, data: unknown) {
	let script = document.getElementById(id) as HTMLScriptElement | null;

	if (!script) {
		script = document.createElement("script");
		script.id = id;
		script.type = "application/ld+json";
		document.head.append(script);
	}

	script.textContent = JSON.stringify(data);
}

export function useHeadMetadata(locale: LocaleCode, messages: LocaleMessages) {
	useEffect(() => {
		const canonicalUrl = getAbsoluteUrl(locale);
		const localeLinks = SUPPORTED_LOCALES.map((supportedLocale) => ({
			hrefLang: supportedLocale,
			href: getAbsoluteUrl(supportedLocale),
		}));

		document.documentElement.lang = locale;
		document.documentElement.dir = isRtlLocale(locale) ? "rtl" : "ltr";
		document.title = messages.seo.title;

		upsertMeta('meta[name="description"]', {
			name: "description",
			content: messages.seo.description,
		});
		upsertMeta('meta[name="robots"]', {
			name: "robots",
			content: "index,follow,max-image-preview:large,max-snippet:-1,max-video-preview:-1",
		});
		upsertMeta('meta[property="og:type"]', {
			property: "og:type",
			content: "website",
		});
		upsertMeta('meta[property="og:title"]', {
			property: "og:title",
			content: messages.seo.openGraphTitle,
		});
		upsertMeta('meta[property="og:description"]', {
			property: "og:description",
			content: messages.seo.openGraphDescription,
		});
		upsertMeta('meta[property="og:url"]', {
			property: "og:url",
			content: canonicalUrl,
		});
		upsertMeta('meta[property="og:site_name"]', {
			property: "og:site_name",
			content: "HTWind",
		});
		upsertMeta('meta[property="og:locale"]', {
			property: "og:locale",
			content: messages.meta.ogLocale,
		});
		upsertMetaArray(
			'property="og:locale:alternate"',
			"property",
			SUPPORTED_LOCALES.filter(
				(supportedLocale) => supportedLocale !== locale,
			).map(
				(supportedLocale) =>
					INTL_LOCALE_MAP[supportedLocale].replace("-", "_"),
			),
			"og-locale-alternate",
		);
		upsertMeta('meta[property="og:image"]', {
			property: "og:image",
			content: OG_IMAGE_URL,
		});
		upsertMeta('meta[property="og:image:width"]', {
			property: "og:image:width",
			content: "1280",
		});
		upsertMeta('meta[property="og:image:height"]', {
			property: "og:image:height",
			content: "720",
		});
		upsertMeta('meta[property="og:image:alt"]', {
			property: "og:image:alt",
			content: messages.seo.imageAlt,
		});
		upsertMeta('meta[name="twitter:card"]', {
			name: "twitter:card",
			content: "summary_large_image",
		});
		upsertMeta('meta[name="twitter:title"]', {
			name: "twitter:title",
			content: messages.seo.twitterTitle,
		});
		upsertMeta('meta[name="twitter:description"]', {
			name: "twitter:description",
			content: messages.seo.twitterDescription,
		});
		upsertMeta('meta[name="twitter:image"]', {
			name: "twitter:image",
			content: OG_IMAGE_URL,
		});
		upsertMeta('meta[name="twitter:image:alt"]', {
			name: "twitter:image:alt",
			content: messages.seo.imageAlt,
		});

		upsertLink('link[rel="canonical"]', {
			rel: "canonical",
			href: canonicalUrl,
		});

		document.head
			.querySelectorAll('link[rel="alternate"][data-managed-hreflang="true"]')
			.forEach((linkNode) => {
				linkNode.remove();
			});

		for (const localeLink of localeLinks) {
			const alternateLink = document.createElement("link");
			alternateLink.setAttribute("rel", "alternate");
			alternateLink.setAttribute("hreflang", localeLink.hrefLang);
			alternateLink.setAttribute("href", localeLink.href);
			alternateLink.setAttribute("data-managed-hreflang", "true");
			document.head.append(alternateLink);
		}

		const defaultLink = document.createElement("link");
		defaultLink.setAttribute("rel", "alternate");
		defaultLink.setAttribute("hreflang", "x-default");
		defaultLink.setAttribute("href", DEFAULT_CANONICAL_URL);
		defaultLink.setAttribute("data-managed-hreflang", "true");
		document.head.append(defaultLink);

		upsertJsonLd("seo-structured-data-software", {
			"@context": "https://schema.org",
			"@type": "SoftwareApplication",
			name: "HTWind",
			applicationCategory: "UtilitiesApplication",
			operatingSystem: "Windows 10, Windows 11",
			inLanguage: locale,
			url: canonicalUrl,
			downloadUrl: RELEASES_URL,
			image: OG_IMAGE_URL,
			description: messages.seo.jsonLdDescription,
			softwareVersion: "latest",
			author: {
				"@type": "Person",
				name: "sametcn99",
			},
			sameAs: [GITHUB_REPOSITORY_URL],
		});

		upsertJsonLd("seo-structured-data-website", {
			"@context": "https://schema.org",
			"@type": "WebSite",
			name: "HTWind",
			url: SITE_URL,
			inLanguage: locale,
			publisher: {
				"@type": "Organization",
				name: "HTWind",
				url: SITE_URL,
			},
		});

		upsertJsonLd("seo-structured-data-organization", {
			"@context": "https://schema.org",
			"@type": "Organization",
			name: "HTWind",
			url: SITE_URL,
			logo: OG_IMAGE_URL,
			sameAs: [GITHUB_REPOSITORY_URL],
		});

		upsertJsonLd("seo-structured-data-faq", {
			"@context": "https://schema.org",
			"@type": "FAQPage",
			inLanguage: locale,
			mainEntity: messages.content.faqItems.map((faq) => ({
				"@type": "Question",
				name: faq.question,
				acceptedAnswer: {
					"@type": "Answer",
					text: faq.answer,
				},
			})),
		});
	}, [locale, messages]);
}
