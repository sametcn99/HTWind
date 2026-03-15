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
import { useLocale } from "../../i18n/LocaleContext";
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
	const { messages } = useLocale();

	return (
		<Card className={styles.heroCard}>
			<div className={styles.heroTopRow}>
				<div className={styles.heroCopyBlock}>
					<Caption1 className={styles.heroEyebrow}>
						{messages.hero.eyebrow}
					</Caption1>
					<h1 className={styles.heroHeading}>{messages.hero.heading}</h1>
					<p className={styles.heroDescription}>{messages.hero.description}</p>
					<div className={styles.heroProofList}>
						{messages.hero.proofPoints.map((item) => (
							<div key={item} className={styles.heroProofItem}>
								<Caption1 className={styles.releaseTitle}>
									{messages.hero.includedLabel}
								</Caption1>
								<span>{item}</span>
							</div>
						))}
					</div>

					<div className={styles.buttonRow}>
						<DownloadMenu
							isLoadingRelease={isLoadingRelease}
							latestAssets={latestAssets}
						/>

						<Button
							as="a"
							href={GITHUB_REPOSITORY_URL}
							target="_blank"
							rel="noreferrer"
							icon={<Code24Regular />}
							className={styles.ghostButton}
							size="large"
						>
							{messages.hero.githubButton}
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
							{messages.hero.releaseNotesButton}
						</Button>
					</div>

					<Body1 className={styles.communityNotice}>
						{messages.hero.communityNotice}
					</Body1>
				</div>

				<aside className={styles.releaseCard} aria-live="polite">
					<Caption1 className={styles.releaseTitle}>
						{messages.hero.releaseCard.title}
					</Caption1>
					<span className={styles.releaseValue}>
						{isLoadingRelease ? (
							<Spinner
								size="tiny"
								labelPosition="after"
								label={messages.hero.releaseCard.loadingLabel}
							/>
						) : (
							releaseText
						)}
					</span>
					<Caption1 className={styles.releaseMeta}>{releaseDate}</Caption1>
					<div className={styles.releaseChannelList}>
						{messages.hero.releaseCard.channels.map((channel) => (
							<div key={channel.name} className={styles.releaseChannelItem}>
								<span className={styles.releaseChannelLabel}>
									{channel.name}
								</span>
								<Caption1 className={styles.dropdownItemMeta}>
									{channel.description}
								</Caption1>
							</div>
						))}
					</div>
					<div className={styles.storeBadgeContainer}>
						<a
							href={MICROSOFT_STORE_URL}
							target="_blank"
							rel="noopener noreferrer"
							aria-label={messages.hero.releaseCard.storeAriaLabel}
						>
							<img
								src={MICROSOFT_STORE_BADGE_URL}
								width="160"
								alt={messages.hero.releaseCard.storeImageAlt}
								className={styles.storeBadgeImage}
							/>
						</a>
					</div>
					<Caption1 className={styles.releaseMeta}>
						{messages.hero.releaseCard.communityPrefix}{" "}
						<Link
							href={GITHUB_DISCUSSIONS_URL}
							target="_blank"
							rel="noreferrer"
						>
							{messages.hero.releaseCard.communityGithubLabel}
						</Link>{" "}
						{messages.hero.releaseCard.communityConnector}{" "}
						<Link href={REDDIT_COMMUNITY_URL} target="_blank" rel="noreferrer">
							{messages.hero.releaseCard.communityRedditLabel}
						</Link>
						{messages.hero.releaseCard.communitySuffix}
					</Caption1>
				</aside>
			</div>
		</Card>
	);
}
