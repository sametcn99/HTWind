import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { LATEST_RELEASE_API_URL, RELEASE_CACHE_KEY } from "../config/constants";
import type { CachedReleaseRecord, GitHubRelease } from "../config/types";
import { interpolate } from "../i18n/interpolate";
import { getLocaleMessages } from "../i18n/resources";
import { formatReleaseDate } from "../lib/formatters";
import { useLatestRelease } from "./useLatestRelease";

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
		{
			id: 2,
			name: "checksums.txt",
			size: 1024,
			browser_download_url: "",
		},
	],
};

const enMessages = getLocaleMessages("en");

function mockFetchOk(release: GitHubRelease): void {
	vi.stubGlobal(
		"fetch",
		vi.fn(
			async () =>
				({ ok: true, json: async () => release }) as unknown as Response,
		),
	);
}

function mockFetchError(status: number): void {
	vi.stubGlobal(
		"fetch",
		vi.fn(async () => ({ ok: false, status }) as unknown as Response),
	);
}

function seedCache(record: CachedReleaseRecord): void {
	window.localStorage.setItem(RELEASE_CACHE_KEY, JSON.stringify(record));
}

async function advanceTimers(ms: number): Promise<void> {
	await act(async () => {
		await vi.advanceTimersByTimeAsync(ms);
	});
}

beforeEach(() => {
	vi.useFakeTimers();
	window.localStorage.clear();
});

afterEach(() => {
	cleanup();
	vi.unstubAllGlobals();
	vi.useRealTimers();
	vi.restoreAllMocks();
	window.localStorage.clear();
});

describe("useLatestRelease", () => {
	it("reports loading state until the delayed fetch completes", async () => {
		mockFetchOk(sampleRelease);

		const { result } = renderHook(() => useLatestRelease("en", enMessages));

		expect(result.current.isLoadingRelease).toBe(true);
		expect(result.current.releaseText).toBe(enMessages.release.loadingLatest);
		expect(vi.mocked(fetch)).not.toHaveBeenCalled();

		await advanceTimers(400);

		expect(result.current.isLoadingRelease).toBe(false);
	});

	it("fetches the latest release and exposes tag, date, and assets", async () => {
		mockFetchOk(sampleRelease);

		const { result } = renderHook(() => useLatestRelease("en", enMessages));
		await advanceTimers(400);

		expect(vi.mocked(fetch)).toHaveBeenCalledWith(
			LATEST_RELEASE_API_URL,
			expect.objectContaining({
				headers: { Accept: "application/vnd.github+json" },
			}),
		);
		expect(result.current.latestRelease?.tag_name).toBe(sampleRelease.tag_name);
		expect(result.current.releaseText).toBe(sampleRelease.tag_name);
		expect(result.current.releaseDate).toBe(
			enMessages.release.publishedOn.replace(
				"{date}",
				formatReleaseDate(
					sampleRelease.published_at,
					"en-US",
					enMessages.release.unknownDate,
				),
			),
		);
		expect(result.current.latestAssets).toHaveLength(1);
		expect(result.current.latestAssets[0]?.name).toBe("HTWind-1.4.0.exe");
	});

	it("writes a successful response into the release cache", async () => {
		mockFetchOk(sampleRelease);

		renderHook(() => useLatestRelease("en", enMessages));
		await advanceTimers(400);

		const raw = window.localStorage.getItem(RELEASE_CACHE_KEY);
		expect(raw).not.toBeNull();
		const cached = JSON.parse(raw ?? "null") as CachedReleaseRecord;
		expect(cached.release.tag_name).toBe(sampleRelease.tag_name);
	});

	it("starts from a valid cache without showing the loading state", async () => {
		seedCache({ savedAt: Date.now(), release: sampleRelease });
		mockFetchError(500);

		const { result } = renderHook(() => useLatestRelease("en", enMessages));

		expect(result.current.isLoadingRelease).toBe(false);
		expect(result.current.latestRelease?.tag_name).toBe(sampleRelease.tag_name);
		expect(result.current.releaseText).toBe(sampleRelease.tag_name);

		await advanceTimers(1500);

		expect(result.current.isLoadingRelease).toBe(false);
		expect(result.current.latestRelease?.tag_name).toBe(sampleRelease.tag_name);
	});

	it("falls back to the cached release when the fetch fails", async () => {
		seedCache({ savedAt: Date.now(), release: sampleRelease });
		mockFetchError(500);

		const { result } = renderHook(() => useLatestRelease("en", enMessages));
		await advanceTimers(1500);

		expect(result.current.isLoadingRelease).toBe(false);
		expect(result.current.latestRelease?.tag_name).toBe(sampleRelease.tag_name);
	});

	it("shows the unavailable state when the fetch fails without a cache", async () => {
		mockFetchError(500);

		const { result } = renderHook(() => useLatestRelease("en", enMessages));
		await advanceTimers(400);

		expect(result.current.isLoadingRelease).toBe(false);
		expect(result.current.latestRelease).toBeNull();
		expect(result.current.releaseText).toBe(enMessages.release.unavailable);
		expect(result.current.releaseDate).toBe(enMessages.release.checkFeed);
		expect(result.current.latestAssets).toHaveLength(0);
	});

	it("interpolates the localized published-on template", async () => {
		mockFetchOk(sampleRelease);

		const trMessages = getLocaleMessages("tr");
		const { result } = renderHook(() => useLatestRelease("tr", trMessages));
		await advanceTimers(400);

		expect(result.current.releaseDate).toBe(
			interpolate(trMessages.release.publishedOn, {
				date: formatReleaseDate(
					sampleRelease.published_at,
					"tr-TR",
					trMessages.release.unknownDate,
				),
			}),
		);
	});
});
