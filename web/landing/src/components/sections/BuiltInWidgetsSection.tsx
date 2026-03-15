import { Badge, Body1, Card, Link } from "@fluentui/react-components";
import {
	GITHUB_REPOSITORY_URL,
	WIDGET_PROMPT_SHARED_URL,
	WIDGET_PROMPT_URL,
} from "../../config/constants";
import { useLocale } from "../../i18n/LocaleContext";
import { useAppStyles } from "../../styles/appStyles";

export function BuiltInWidgetsSection() {
	const styles = useAppStyles();
	const { messages } = useLocale();

	return (
		<section aria-labelledby="built-in-widgets-heading">
			<Card className={styles.widgetsCard}>
				<h2 id="built-in-widgets-heading" className={styles.sectionHeading}>
					{messages.sections.widgets.heading}
				</h2>
				<p className={styles.sectionLead}>{messages.sections.widgets.lead}</p>
				<div className={styles.widgetList}>
					{messages.content.widgets.map((widget) => (
						<Badge
							key={widget.id}
							size="large"
							appearance="tint"
							color="informative"
							className={styles.widgetBadge}
						>
							{widget.label}
						</Badge>
					))}
				</div>
				<Body1 className={styles.featureDescription}>
					{messages.sections.widgets.documentationPrefix}{" "}
					<Link
						href={`${GITHUB_REPOSITORY_URL}#built-in-widgets`}
						target="_blank"
						rel="noreferrer"
					>
						{messages.sections.widgets.documentationLinkLabel}
					</Link>
					{messages.sections.widgets.documentationSuffix}
				</Body1>
				<Body1 className={styles.featureDescription}>
					{messages.sections.widgets.promptPrefix}{" "}
					<Link href={WIDGET_PROMPT_URL} target="_blank" rel="noreferrer">
						{messages.sections.widgets.promptLabel}
					</Link>{" "}
					{messages.sections.widgets.promptConnector}{" "}
					<Link
						href={WIDGET_PROMPT_SHARED_URL}
						target="_blank"
						rel="noreferrer"
					>
						{messages.sections.widgets.sharedPromptLabel}
					</Link>{" "}
					{messages.sections.widgets.promptSuffix}
				</Body1>
			</Card>
		</section>
	);
}
