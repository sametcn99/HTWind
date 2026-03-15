import { Caption1, Card, mergeClasses } from "@fluentui/react-components";
import { useLocale } from "../../i18n/LocaleContext";
import { useAppStyles } from "../../styles/appStyles";

export function HighlightsSection() {
	const styles = useAppStyles();
	const { messages } = useLocale();

	return (
		<section aria-labelledby="htwind-highlights-heading">
			<Card className={styles.longFormSection}>
				<h2 id="htwind-highlights-heading" className={styles.sectionHeading}>
					{messages.sections.highlights.heading}
				</h2>
				<p className={styles.sectionLead}>
					{messages.sections.highlights.lead}
				</p>
				<div className={styles.statsGrid}>
					{messages.content.highlights.map((item) => (
						<Card
							key={item.label}
							className={mergeClasses(
								styles.statCard,
								item.spanTwo && styles.statCardSpanTwo,
							)}
						>
							<span className={styles.statValue}>{item.value}</span>
							<Caption1 className={styles.statLabel}>{item.label}</Caption1>
						</Card>
					))}
				</div>
			</Card>
		</section>
	);
}
