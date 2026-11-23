import type { GitHubRelease } from "../config/types";

export function isGitHubRelease(value: unknown): value is GitHubRelease {
	if (typeof value !== "object" || value === null) {
		return false;
	}
	const release = value as Record<string, unknown>;
	return (
		typeof release.tag_name === "string" &&
		release.tag_name.trim().length > 0 &&
		(release.name === null || typeof release.name === "string") &&
		(release.published_at === null ||
			typeof release.published_at === "string") &&
		typeof release.html_url === "string" &&
		(release.assets === undefined ||
			(Array.isArray(release.assets) &&
				release.assets.every((asset: unknown) => {
					if (typeof asset !== "object" || asset === null) {
						return false;
					}
					const item = asset as Record<string, unknown>;
					return (
						typeof item.id === "number" &&
						Number.isFinite(item.id) &&
						typeof item.name === "string" &&
						typeof item.size === "number" &&
						Number.isFinite(item.size) &&
						item.size >= 0 &&
						typeof item.browser_download_url === "string"
					);
				})))
	);
}
