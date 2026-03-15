import { Button, Caption1, Link } from "@fluentui/react-components";
import { ArrowDownload24Regular } from "@fluentui/react-icons";
import type { GitHubReleaseAsset } from "../../config/types";
import { useDownloadMenu } from "../../hooks/useDownloadMenu";
import { formatFileSize } from "../../lib/formatters";
import { useAppStyles } from "../../styles/appStyles";

type DownloadMenuProps = {
	isLoadingRelease: boolean;
	latestAssets: GitHubReleaseAsset[];
};

export function DownloadMenu({
	isLoadingRelease,
	latestAssets,
}: DownloadMenuProps) {
	const styles = useAppStyles();
	const { containerRef, isOpen, toggle, close } = useDownloadMenu();

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
					? "Loading assets..."
					: latestAssets.length === 0
						? "No downloadable assets"
						: `Download Latest (${latestAssets.length})`}
			</Button>

			{isOpen && latestAssets.length > 0 && (
				<div className={styles.dropdownMenu}>
					<Caption1 className={styles.dropdownTitle}>
						Latest Release Assets
					</Caption1>
					<ul
						className={styles.dropdownList}
						aria-label="Latest release asset downloads"
					>
						{latestAssets.map((asset) => (
							<li key={asset.id}>
								<Link
									href={asset.browser_download_url}
									target="_blank"
									rel="noreferrer"
									className={styles.dropdownItem}
									onClick={close}
								>
									<span>{asset.name}</span>
									<Caption1 className={styles.dropdownItemMeta}>
										{formatFileSize(asset.size)}
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
