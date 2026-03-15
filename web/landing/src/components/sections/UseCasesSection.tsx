import { Card } from "@fluentui/react-components";
import { workflowUseCases } from "../../config/content";
import { useAppStyles } from "../../styles/appStyles";

export function UseCasesSection() {
	const styles = useAppStyles();

	return (
		<section aria-labelledby="htwind-use-cases-heading">
			<Card className={styles.longFormSection}>
				<h2 id="htwind-use-cases-heading" className={styles.sectionHeading}>
					Common HTWind use cases
				</h2>
				<p className={styles.sectionLead}>
					The following workflows highlight how HTWind can be used as a
					practical Windows 11 widget platform in both personal productivity and
					technical operations contexts.
				</p>
				<ul className={styles.bulletList}>
					{workflowUseCases.map((item) => (
						<li key={item}>{item}</li>
					))}
				</ul>
			</Card>
		</section>
	);
}
