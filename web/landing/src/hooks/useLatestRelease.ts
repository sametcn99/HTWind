import { useEffect, useMemo, useState } from "react";
import { LATEST_RELEASE_API_URL } from "../config/constants";
import type { GitHubRelease } from "../config/types";
import { formatReleaseDate } from "../lib/formatters";
import { readCachedRelease, writeCachedRelease } from "../lib/storage";

export function useLatestRelease() {
	const [latestRelease, setLatestRelease] = useState<GitHubRelease | null>(
		null,
	);
	const [isLoadingRelease, setIsLoadingRelease] = useState(true);

	useEffect(() => {
		const abortController = new AbortController();
		const cachedRelease = readCachedRelease();

		if (cachedRelease) {
			setLatestRelease(cachedRelease);
			setIsLoadingRelease(false);
		}

		async function loadLatestRelease(): Promise<void> {
			try {
				const response = await fetch(LATEST_RELEASE_API_URL, {
					signal: abortController.signal,
					headers: {
						Accept: "application/vnd.github+json",
					},
				});

				if (!response.ok) {
					throw new Error(`GitHub API failed with status ${response.status}`);
				}

				const payload = (await response.json()) as GitHubRelease;
				setLatestRelease(payload);
				writeCachedRelease(payload);
			} catch {
				if (!cachedRelease) {
					setLatestRelease(null);
				}
			} finally {
				setIsLoadingRelease(false);
			}
		}

		const fetchTimer = window.setTimeout(
			() => {
				void loadLatestRelease();
			},
			cachedRelease ? 1200 : 350,
		);

		return () => {
			window.clearTimeout(fetchTimer);
			abortController.abort();
		};
	}, []);

	const releaseText = useMemo(() => {
		if (isLoadingRelease) {
			return "Loading latest release...";
		}

		if (!latestRelease?.tag_name) {
			return "Release info unavailable";
		}

		return latestRelease.tag_name;
	}, [isLoadingRelease, latestRelease]);

	const releaseDate = useMemo(() => {
		if (!latestRelease?.published_at) {
			return "Check release feed for updates";
		}

		return `Published ${formatReleaseDate(latestRelease.published_at)}`;
	}, [latestRelease]);

	const latestAssets = useMemo(() => {
		if (!latestRelease?.assets?.length) {
			return [];
		}

		return latestRelease.assets.filter((asset) =>
			Boolean(asset.browser_download_url),
		);
	}, [latestRelease]);

	return {
		latestRelease,
		isLoadingRelease,
		releaseText,
		releaseDate,
		latestAssets,
	};
}
