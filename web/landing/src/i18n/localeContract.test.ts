import { describe, expect, it } from "vitest";
import { INTL_LOCALE_MAP, LOCALE_SEGMENTS, SUPPORTED_LOCALES } from "./locales";
import { getLocaleMessages, resources } from "./resources";

type FlattenedMessages = Map<string, string>;

const PLACEHOLDER_PATTERN = /\{(\w+)\}/g;

function extractPlaceholders(value: string): string[] {
	const placeholders = [...value.matchAll(PLACEHOLDER_PATTERN)].map(
		(match) => match[1],
	);
	return [...placeholders].sort();
}

function flattenMessages(
	value: unknown,
	prefix: string,
	leaves: FlattenedMessages,
): void {
	if (typeof value === "string") {
		leaves.set(prefix, value);
		return;
	}

	if (Array.isArray(value)) {
		value.forEach((item, index) => {
			flattenMessages(item, `${prefix}.${index}`, leaves);
		});
		return;
	}

	if (value === null || value === undefined) {
		return;
	}

	for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
		flattenMessages(child, prefix ? `${prefix}.${key}` : key, leaves);
	}
}

function flattenLocale(locale: (typeof SUPPORTED_LOCALES)[number]) {
	const leaves: FlattenedMessages = new Map();
	flattenMessages(getLocaleMessages(locale), "", leaves);
	return leaves;
}

describe("locale message contract", () => {
	const enLeaves = flattenLocale("en");

	it("registers exactly the supported locales", () => {
		expect(Object.keys(resources).sort()).toEqual(
			[...SUPPORTED_LOCALES].sort(),
		);
	});

	it("maps every locale in the segment and Intl lookup tables", () => {
		for (const locale of SUPPORTED_LOCALES) {
			expect(LOCALE_SEGMENTS[locale]).toBe(locale);
			expect(INTL_LOCALE_MAP[locale]).toMatch(new RegExp(`^${locale}[-_]`));
		}
	});

	it.each([
		...SUPPORTED_LOCALES,
	])("%s has the same key structure as en", (locale) => {
		const leaves = flattenLocale(locale);
		expect([...leaves.keys()].sort()).toEqual([...enLeaves.keys()].sort());
	});

	it.each([
		...SUPPORTED_LOCALES,
	])("%s has no empty message strings", (locale) => {
		const leaves = flattenLocale(locale);
		for (const value of leaves.values()) {
			expect(value.trim().length).toBeGreaterThan(0);
		}
	});

	it.each([
		...SUPPORTED_LOCALES,
	])("%s uses the same interpolation placeholders as en", (locale) => {
		const leaves = flattenLocale(locale);
		for (const [path, enValue] of enLeaves) {
			const localeValue = leaves.get(path);
			if (!localeValue) {
				continue;
			}

			expect(extractPlaceholders(localeValue)).toEqual(
				extractPlaceholders(enValue),
			);
		}
	});
});
