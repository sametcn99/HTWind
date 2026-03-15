import {
	Button,
	Caption1,
	Link,
	mergeClasses,
} from "@fluentui/react-components";
import { ArrowDownload24Regular } from "@fluentui/react-icons";
import type { GitHubReleaseAsset } from "../../config/types";
import { useDownloadMenu } from "../../hooks/useDownloadMenu";
import { interpolate } from "../../i18n/interpolate";
import { useLocale } from "../../i18n/LocaleContext";
import { INTL_LOCALE_MAP } from "../../i18n/locales";
import { formatFileSize } from "../../lib/formatters";
import { useAppStyles } from "../../styles/appStyles";

type DownloadMenuProps = {
	isLoadingRelease: boolean;
	latestAssets: GitHubReleaseAsset[];
};

type AssetDescriptionKey = "installer" | "portable" | "package" | "other";

function getAssetRank(asset: GitHubReleaseAsset): number {
	const normalizedName = asset.name.toLowerCase();

	if (
		normalizedName.includes("setup") ||
		normalizedName.endsWith(".exe") ||
		normalizedName.endsWith(".msi")
	) {
		return 0;
	}

	if (normalizedName.endsWith(".zip")) {
		return 1;
	}

	if (
		normalizedName.endsWith(".msix") ||
		normalizedName.endsWith(".appinstaller")
	) {
		return 2;
	}

	return 3;
}

function getAssetDescriptionKey(
	asset: GitHubReleaseAsset,
): AssetDescriptionKey {
	const normalizedName = asset.name.toLowerCase();

	if (
		normalizedName.includes("setup") ||
		normalizedName.endsWith(".exe") ||
		normalizedName.endsWith(".msi")
	) {
		return "installer";
	}

	if (normalizedName.endsWith(".zip")) {
		return "portable";
	}

	if (
		normalizedName.endsWith(".msix") ||
		normalizedName.endsWith(".appinstaller")
	) {
		return "package";
	}

	return "other";
}

export function DownloadMenu({
	isLoadingRelease,
	latestAssets,
}: DownloadMenuProps) {
	const styles = useAppStyles();
	const { locale, messages } = useLocale();
	const { containerRef, isOpen, toggle, close } = useDownloadMenu();
	const sortedAssets = [...latestAssets].sort(
		(leftAsset, rightAsset) =>
			getAssetRank(leftAsset) - getAssetRank(rightAsset) ||
			leftAsset.name.localeCompare(rightAsset.name),
	);

	return (
		<div ref={containerRef} className={styles.dropdownContainer}>
			<Button
				icon={<ArrowDownload24Regular />}
				className={styles.dropdownButton}
				size="large"
				disabled={isLoadingRelease || latestAssets.length === 0}
				onClick={toggle}
				aria-expanded={isOpen}
				aria-haspopup="menu"
			>
				{isLoadingRelease
					? messages.downloadMenu.loading
					: latestAssets.length === 0
						? messages.downloadMenu.empty
						: interpolate(messages.downloadMenu.choose, {
								count: latestAssets.length,
							})}
			</Button>

			{isOpen && latestAssets.length > 0 && (
				<div className={styles.dropdownMenu}>
					<Caption1 className={styles.dropdownTitle}>
						{messages.downloadMenu.title}
					</Caption1>
					<Caption1 className={styles.dropdownHint}>
						{messages.downloadMenu.hint}
					</Caption1>
					<ul
						className={styles.dropdownList}
						aria-label={messages.downloadMenu.ariaLabel}
					>
						{sortedAssets.map((asset) => (
							<li key={asset.id}>
								<Link
									href={asset.browser_download_url}
									target="_blank"
									rel="noreferrer"
									className={mergeClasses(
										styles.dropdownItem,
										getAssetRank(asset) === 0 && styles.dropdownItemRecommended,
									)}
									onClick={close}
								>
									<span className={styles.dropdownItemTitle}>{asset.name}</span>
									<Caption1 className={styles.dropdownItemDescription}>
										{
											messages.downloadMenu.descriptions[
												getAssetDescriptionKey(asset)
											]
										}
									</Caption1>
									<Caption1 className={styles.dropdownItemMeta}>
										{formatFileSize(
											asset.size,
											INTL_LOCALE_MAP[locale],
											messages.downloadMenu.unknownSize,
										)}
									</Caption1>
								</Link>
							</li>
						))}
					</ul>
				</div>
			)}
		</div>
	);
}
