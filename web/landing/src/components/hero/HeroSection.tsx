import {
	Body1,
	Button,
	Caption1,
	Card,
	Link,
	Spinner,
} from "@fluentui/react-components";
import { Code24Regular, Open24Regular } from "@fluentui/react-icons";
import {
	GITHUB_DISCUSSIONS_URL,
	GITHUB_REPOSITORY_URL,
	MICROSOFT_STORE_BADGE_URL,
	MICROSOFT_STORE_URL,
	REDDIT_COMMUNITY_URL,
	RELEASES_URL,
} from "../../config/constants";
import type { GitHubReleaseAsset } from "../../config/types";
import { useAppStyles } from "../../styles/appStyles";
import { DownloadMenu } from "./DownloadMenu";

type HeroSectionProps = {
	isLoadingRelease: boolean;
	releaseText: string;
	releaseDate: string;
	latestReleaseUrl?: string;
	latestAssets: GitHubReleaseAsset[];
};

export function HeroSection({
	isLoadingRelease,
	releaseText,
	releaseDate,
	latestReleaseUrl,
	latestAssets,
}: HeroSectionProps) {
	const styles = useAppStyles();

	return (
		<Card className={styles.heroCard}>
			<div className={styles.heroTopRow}>
				<div>
					<h1 className={styles.heroHeading}>HTWind for Windows 11 desktops</h1>
					<p className={styles.heroDescription}>
						HTWind is a customizable desktop widget manager that lets you run
						rich HTML widgets, import manifest-based widget packages, monitor
						your system, and execute PowerShell tools from a polished
						Windows-focused workspace.
					</p>
				</div>

				<aside className={styles.releaseCard} aria-live="polite">
					<Caption1 className={styles.releaseTitle}>Latest release</Caption1>
					<span className={styles.releaseValue}>
						{isLoadingRelease ? (
							<Spinner size="tiny" labelPosition="after" label="Loading" />
						) : (
							releaseText
						)}
					</span>
					<Caption1>{releaseDate}</Caption1>
					<div className={styles.storeBadgeContainer}>
						<a
							href={MICROSOFT_STORE_URL}
							target="_blank"
							rel="noopener noreferrer"
							aria-label="Get it from Microsoft Store"
						>
							<img
								src={MICROSOFT_STORE_BADGE_URL}
								width="160"
								alt="Get it from Microsoft Store"
								className={styles.storeBadgeImage}
							/>
						</a>
					</div>
				</aside>
			</div>

			<div className={styles.buttonRow}>
				<Button
					as="a"
					href={GITHUB_REPOSITORY_URL}
					target="_blank"
					rel="noreferrer"
					icon={<Code24Regular />}
					className={styles.primaryButton}
					size="large"
				>
					View on GitHub
				</Button>

				<Button
					as="a"
					href={latestReleaseUrl ?? RELEASES_URL}
					target="_blank"
					rel="noreferrer"
					icon={<Open24Regular />}
					className={styles.ghostButton}
					size="large"
				>
					Open Latest Release
				</Button>

				<DownloadMenu
					isLoadingRelease={isLoadingRelease}
					latestAssets={latestAssets}
				/>
			</div>

			<Body1 className={styles.featureDescription}>
				Recommended installation method: download the installer file (
				<strong>HTWind-setup-&lt;version&gt;.exe</strong>) from the latest
				GitHub release. Portable ZIP and Microsoft Store are available as
				alternative options. Once installed, you can import standalone HTML
				widgets, asset-based widget folders through manifests, or complete
				multi-widget packages.
			</Body1>

			<Body1 className={styles.communityNotice}>
				You can share your custom widgets with the community in{" "}
				<Link href={GITHUB_DISCUSSIONS_URL} target="_blank" rel="noreferrer">
					GitHub Discussions
				</Link>{" "}
				and{" "}
				<Link href={REDDIT_COMMUNITY_URL} target="_blank" rel="noreferrer">
					Reddit
				</Link>
				, discover ready-to-use examples, and get feedback from other HTWind
				users.
			</Body1>
		</Card>
	);
}
