import { RELEASE_CACHE_KEY, RELEASE_CACHE_TTL_MS } from "../config/constants";
import type { CachedReleaseRecord, GitHubRelease } from "../config/types";

export function readCachedRelease(): GitHubRelease | null {
	if (typeof window === "undefined") {
		return null;
	}

	try {
		const raw = window.localStorage.getItem(RELEASE_CACHE_KEY);
		if (!raw) {
			return null;
		}

		const cached = JSON.parse(raw) as CachedReleaseRecord;
		if (!cached?.savedAt || !cached?.release) {
			return null;
		}

		if (Date.now() - cached.savedAt > RELEASE_CACHE_TTL_MS) {
			window.localStorage.removeItem(RELEASE_CACHE_KEY);
			return null;
		}

		return cached.release;
	} catch {
		return null;
	}
}

export function writeCachedRelease(release: GitHubRelease): void {
	if (typeof window === "undefined") {
		return;
	}

	try {
		const record: CachedReleaseRecord = {
			savedAt: Date.now(),
			release,
		};

		window.localStorage.setItem(RELEASE_CACHE_KEY, JSON.stringify(record));
	} catch {
		// Ignore storage errors to avoid blocking UI rendering.
	}
}
