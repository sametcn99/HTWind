import { Card } from "@fluentui/react-components";
import { useLocale } from "../../i18n/LocaleContext";
import { useAppStyles } from "../../styles/appStyles";

export function UseCasesSection() {
	const styles = useAppStyles();
	const { messages } = useLocale();

	return (
		<section aria-labelledby="htwind-use-cases-heading">
			<Card className={styles.longFormSection}>
				<h2 id="htwind-use-cases-heading" className={styles.sectionHeading}>
					{messages.sections.useCases.heading}
				</h2>
				<p className={styles.sectionLead}>{messages.sections.useCases.lead}</p>
				<ul className={styles.bulletList}>
					{messages.content.workflowUseCases.map((item) => (
						<li key={item}>{item}</li>
					))}
				</ul>
			</Card>
		</section>
	);
}
