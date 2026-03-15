import { Card } from "@fluentui/react-components";
import { faqItems } from "../../config/content";
import { useAppStyles } from "../../styles/appStyles";

export function FaqSection() {
	const styles = useAppStyles();

	return (
		<section aria-labelledby="htwind-faq-heading">
			<Card className={styles.longFormSection}>
				<h2 id="htwind-faq-heading" className={styles.sectionHeading}>
					HTWind FAQ
				</h2>
				<p className={styles.sectionLead}>
					Quick answers for users searching for a Windows HTML widget manager
					with PowerShell integration and open-source customization.
				</p>
				<div className={styles.faqGrid}>
					{faqItems.map((faq) => (
						<article
							key={faq.question}
							className={
								faq.spanTwoColumns
									? `${styles.faqCard} ${styles.faqCardWide}`
									: styles.faqCard
							}
						>
							<h3 className={styles.faqQuestion}>{faq.question}</h3>
							<p className={styles.faqAnswer}>{faq.answer}</p>
						</article>
					))}
				</div>
			</Card>
		</section>
	);
}
