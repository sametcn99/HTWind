import { Body1, Card, Link } from "@fluentui/react-components";
import {
	GITHUB_DISCUSSIONS_URL,
	REDDIT_COMMUNITY_URL,
} from "../../config/constants";
import { useLocale } from "../../i18n/LocaleContext";
import { useAppStyles } from "../../styles/appStyles";

export function CommunitySection() {
	const styles = useAppStyles();
	const { messages } = useLocale();

	return (
		<section aria-labelledby="htwind-community-sharing-heading">
			<Card className={styles.communitySection}>
				<h2
					id="htwind-community-sharing-heading"
					className={styles.sectionHeading}
				>
					{messages.sections.community.heading}
				</h2>
				<p className={styles.sectionLead}>{messages.sections.community.lead}</p>
				<ul className={styles.bulletList}>
					{messages.content.communitySharingSteps.map((item) => (
						<li key={item}>{item}</li>
					))}
				</ul>
				<Body1>
					{messages.sections.community.visitPrefix}{" "}
					<Link href={GITHUB_DISCUSSIONS_URL} target="_blank" rel="noreferrer">
						{messages.sections.community.githubLinkLabel}
					</Link>{" "}
					{messages.sections.community.visitConnector}{" "}
					<Link href={REDDIT_COMMUNITY_URL} target="_blank" rel="noreferrer">
						{messages.sections.community.redditLinkLabel}
					</Link>{" "}
					{messages.sections.community.visitSuffix}
				</Body1>
			</Card>
		</section>
	);
}
