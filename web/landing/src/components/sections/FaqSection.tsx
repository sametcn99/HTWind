import { Card } from "@fluentui/react-components";
import { useLocale } from "../../i18n/LocaleContext";
import { useAppStyles } from "../../styles/appStyles";

export function FaqSection() {
	const styles = useAppStyles();
	const { messages } = useLocale();

	return (
		<section aria-labelledby="htwind-faq-heading">
			<Card className={styles.longFormSection}>
				<h2 id="htwind-faq-heading" className={styles.sectionHeading}>
					{messages.sections.faq.heading}
				</h2>
				<p className={styles.sectionLead}>{messages.sections.faq.lead}</p>
				<div className={styles.faqGrid}>
					{messages.content.faqItems.map((faq) => (
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
