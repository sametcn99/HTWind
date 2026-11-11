import { afterEach, describe, expect, it, vi } from "vitest";
import { RELEASE_CACHE_KEY, RELEASE_CACHE_TTL_MS } from "../config/constants";
import type { CachedReleaseRecord, GitHubRelease } from "../config/types";
import { LOCALE_STORAGE_KEY } from "../i18n/locales";
import {
	readCachedRelease,
	readSavedLocale,
	writeCachedRelease,
	writeSavedLocale,
} from "./storage";

const sampleRelease: GitHubRelease = {
	tag_name: "v1.4.0",
	name: "HTWind 1.4.0",
	published_at: "2026-01-02T12:00:00Z",
	html_url: "https://github.com/sametcn99/HTWind/releases/tag/v1.4.0",
	assets: [
		{
			id: 1,
			name: "HTWind-1.4.0.exe",
			size: 1024 * 1024,
			browser_download_url: "https://example.com/HTWind-1.4.0.exe",
		},
	],
};

afterEach(() => {
	vi.restoreAllMocks();
	window.localStorage.clear();
});

describe("release cache", () => {
	it("returns null when nothing is cached", () => {
		expect(readCachedRelease()).toBeNull();
	});

	it("round-trips a release through the cache", () => {
		writeCachedRelease(sampleRelease);

		const cached = readCachedRelease();
		expect(cached).not.toBeNull();
		expect(cached?.tag_name).toBe(sampleRelease.tag_name);
		expect(cached?.assets?.[0]?.name).toBe(sampleRelease.assets?.[0]?.name);
	});

	it("drops expired cache entries and removes the key", () => {
		const expired: CachedReleaseRecord = {
			savedAt: Date.now() - RELEASE_CACHE_TTL_MS - 1000,
			release: sampleRelease,
		};
		window.localStorage.setItem(RELEASE_CACHE_KEY, JSON.stringify(expired));

		expect(readCachedRelease()).toBeNull();
		expect(window.localStorage.getItem(RELEASE_CACHE_KEY)).toBeNull();
	});

	it("rejects malformed cache payloads", () => {
		window.localStorage.setItem(RELEASE_CACHE_KEY, "{{not json");
		expect(readCachedRelease()).toBeNull();

		window.localStorage.setItem(
			RELEASE_CACHE_KEY,
			JSON.stringify({ savedAt: Date.now() }),
		);
		expect(readCachedRelease()).toBeNull();
	});

	it("tolerates a throwing localStorage on write", () => {
		vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
			throw new DOMException("quota exceeded", "QuotaExceededError");
		});

		expect(() => writeCachedRelease(sampleRelease)).not.toThrow();
	});

	it("tolerates a throwing localStorage on read", () => {
		vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
			throw new DOMException("access denied", "SecurityError");
		});

		expect(readCachedRelease()).toBeNull();
	});
});

describe("saved locale", () => {
	it("round-trips a saved locale", () => {
		expect(readSavedLocale()).toBeNull();

		writeSavedLocale("tr");
		expect(readSavedLocale()).toBe("tr");
		expect(window.localStorage.getItem(LOCALE_STORAGE_KEY)).toBe("tr");
	});

	it("normalizes partial locale tags when reading", () => {
		window.localStorage.setItem(LOCALE_STORAGE_KEY, "PT-br");
		expect(readSavedLocale()).toBe("pt");
	});

	it("returns null for unsupported stored values", () => {
		window.localStorage.setItem(LOCALE_STORAGE_KEY, "klingon");
		expect(readSavedLocale()).toBeNull();
	});

	it("tolerates storage failures", () => {
		vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
			throw new DOMException("access denied", "SecurityError");
		});
		vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
			throw new DOMException("quota exceeded", "QuotaExceededError");
		});

		expect(readSavedLocale()).toBeNull();
		expect(() => writeSavedLocale("de")).not.toThrow();
	});
});
