import { RELEASES_URL } from "../../config/constants";
import type { GitHubReleaseAsset, ScreenshotItem } from "../../config/types";
import { interpolate } from "../../i18n/interpolate";
import { useLocale } from "../../i18n/LocaleContext";
import { DownloadMenu } from "./DownloadMenu";

type HeroSectionProps = {
	isLoadingRelease: boolean;
	releaseText: string;
	releaseDate: string;
	latestReleaseUrl?: string;
	latestAssets: GitHubReleaseAsset[];
	onSelectImage: (image: ScreenshotItem) => void;
};

export function HeroSection({
	isLoadingRelease,
	releaseText,
	releaseDate,
	latestReleaseUrl,
	latestAssets,
	onSelectImage,
}: HeroSectionProps) {
	const { messages } = useLocale();
	const screenshot = messages.content.overviewScreenshots[0];
	return (
		<section className="hero wrap" aria-labelledby="hero-heading">
			<div className="hero-copy">
				<p className="eyebrow">
					<span className="status-dot" />
					{messages.design.eyebrow}
				</p>
				<h1 id="hero-heading">{messages.design.heroHeading}</h1>
				<p className="hero-description">{messages.design.heroDescription}</p>
				<DownloadMenu
					isLoadingRelease={isLoadingRelease}
					latestAssets={latestAssets}
				/>
				<div className="release-meta" aria-live="polite">
					<a
						href={latestReleaseUrl ?? RELEASES_URL}
						target="_blank"
						rel="noopener noreferrer"
					>
						{releaseText}
					</a>
					<span>{releaseDate}</span>
				</div>
			</div>
			<figure className="hero-figure">
				<div className="hero-image-mat">
					<button
						type="button"
						className="screenshot-button"
						onClick={() => onSelectImage(screenshot)}
						aria-label={interpolate(messages.sections.overview.viewLarger, {
							alt: screenshot.alt,
						})}
					>
						<img
							className="hero-screenshot"
							src={screenshot.src}
							alt={screenshot.alt}
							width="1380"
							height="990"
							fetchPriority="high"
						/>
					</button>
				</div>
				<figcaption>
					<span>HTWind / Windows</span>
					<span>{messages.design.library}</span>
				</figcaption>
			</figure>
		</section>
	);
}
