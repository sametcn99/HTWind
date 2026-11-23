import { useId } from "react";
import { RELEASES_URL } from "../../config/constants";
import type { GitHubReleaseAsset } from "../../config/types";
import { useDownloadMenu } from "../../hooks/useDownloadMenu";
import { useLocale } from "../../i18n/LocaleContext";
import { INTL_LOCALE_MAP } from "../../i18n/locales";
import { formatFileSize } from "../../lib/formatters";

type DownloadMenuProps = {
	isLoadingRelease: boolean;
	latestAssets: GitHubReleaseAsset[];
};

function assetKind(asset: GitHubReleaseAsset) {
	if (/\.(exe|msi)$/i.test(asset.name)) return "installer";
	if (/\.zip$/i.test(asset.name)) return "portable";
	if (/\.(msix|appinstaller)$/i.test(asset.name)) return "package";
	return "other";
}

export function DownloadMenu({
	isLoadingRelease,
	latestAssets,
}: DownloadMenuProps) {
	const { locale, messages } = useLocale();
	const { containerRef, triggerRef, isOpen, toggle, close } = useDownloadMenu();
	const menuId = useId();
	const priority = { installer: 0, portable: 1, package: 2, other: 3 };
	const assets = [...latestAssets].sort(
		(a, b) =>
			priority[assetKind(a)] - priority[assetKind(b)] ||
			a.name.localeCompare(b.name),
	);
	const installer = assets.find((asset) => assetKind(asset) === "installer");
	return (
		<div className="download-area" ref={containerRef}>
			<div className="download-actions">
				<a
					className="button button-primary"
					href={installer?.browser_download_url ?? RELEASES_URL}
					target="_blank"
					rel="noopener noreferrer"
				>
					{messages.design.download}
					<span aria-hidden="true">↓</span>
				</a>
				<button
					className="download-toggle"
					type="button"
					ref={triggerRef}
					aria-expanded={isOpen}
					aria-controls={menuId}
					disabled={isLoadingRelease || assets.length === 0}
					onClick={toggle}
				>
					{messages.design.otherDownloads}
					<span aria-hidden="true">⌄</span>
				</button>
			</div>
			{isOpen && assets.length > 0 && (
				<div className="download-panel" id={menuId}>
					<p className="download-heading">{messages.downloadMenu.title}</p>
					<ul aria-label={messages.downloadMenu.ariaLabel}>
						{assets.map((asset) => (
							<li key={asset.id}>
								<a
									href={asset.browser_download_url}
									target="_blank"
									rel="noopener noreferrer"
									onClick={close}
								>
									<span>
										<strong>{asset.name}</strong>
										<small>
											{messages.downloadMenu.descriptions[assetKind(asset)]}
										</small>
									</span>
									<span className="file-size">
										{formatFileSize(
											asset.size,
											INTL_LOCALE_MAP[locale],
											messages.downloadMenu.unknownSize,
										)}
									</span>
								</a>
							</li>
						))}
					</ul>
				</div>
			)}
		</div>
	);
}
