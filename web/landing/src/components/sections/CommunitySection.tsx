import { Body1, Card, Link } from "@fluentui/react-components";
import {
	GITHUB_DISCUSSIONS_URL,
	REDDIT_COMMUNITY_URL,
} from "../../config/constants";
import { communitySharingSteps } from "../../config/content";
import { useAppStyles } from "../../styles/appStyles";

export function CommunitySection() {
	const styles = useAppStyles();

	return (
		<section aria-labelledby="htwind-community-sharing-heading">
			<Card className={styles.communitySection}>
				<h2
					id="htwind-community-sharing-heading"
					className={styles.sectionHeading}
				>
					Share your widgets with the HTWind community
				</h2>
				<p className={styles.sectionLead}>
					HTWind supports a community-driven workflow where users publish and
					discuss widget ideas directly in GitHub Discussions and in the HTWind
					Reddit community. This makes it easy to exchange desktop widget
					templates, compare approaches for Windows automation, and improve
					widget quality through real usage feedback.
				</p>
				<p className={styles.sectionLead}>
					If you build a clock variation, a system monitor, a file utility
					panel, or any custom HTML widget, you can post it in both channels so
					others can test and adapt it. Include screenshots, usage notes, and
					integration details to help users quickly adopt your widget in their
					own HTWind setup.
				</p>
				<ul className={styles.bulletList}>
					{communitySharingSteps.map((item) => (
						<li key={item}>{item}</li>
					))}
				</ul>
				<Body1>
					Visit{" "}
					<Link href={GITHUB_DISCUSSIONS_URL} target="_blank" rel="noreferrer">
						HTWind GitHub Discussions
					</Link>{" "}
					and{" "}
					<Link href={REDDIT_COMMUNITY_URL} target="_blank" rel="noreferrer">
						r/HTWind on Reddit
					</Link>{" "}
					to publish widgets, report bugs, request features, and share desktop
					setups.
				</Body1>
			</Card>
		</section>
	);
}
