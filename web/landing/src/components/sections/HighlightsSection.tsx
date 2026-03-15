import { Caption1, Card, mergeClasses } from "@fluentui/react-components";
import { highlights } from "../../config/content";
import { useAppStyles } from "../../styles/appStyles";

export function HighlightsSection() {
	const styles = useAppStyles();

	return (
		<section className={styles.statsGrid} aria-label="Project highlights">
			{highlights.map((item) => (
				<Card
					key={item.label}
					className={mergeClasses(
						styles.statCard,
						item.spanTwo && styles.statCardSpanTwo,
						item.spanFour && styles.statCardSpanFour,
					)}
				>
					<span className={styles.statValue}>{item.value}</span>
					<Caption1 className={styles.statLabel}>{item.label}</Caption1>
				</Card>
			))}
		</section>
	);
}
