import { Button, Card } from "@fluentui/react-components";
import { SUPPORT_URL } from "../../config/constants";
import { useLocale } from "../../i18n/LocaleContext";
import { useAppStyles } from "../../styles/appStyles";

export function SupportSection() {
	const styles = useAppStyles();
	const { messages } = useLocale();

	return (
		<section aria-labelledby="support-developer-heading">
			<Card className={styles.longFormSection}>
				<h2 id="support-developer-heading" className={styles.sectionHeading}>
					{messages.sections.support.heading}
				</h2>
				<p className={styles.sectionLead}>{messages.sections.support.lead}</p>
				<Button
					as="a"
					href={SUPPORT_URL}
					target="_blank"
					rel="noreferrer"
					className={styles.ghostButton}
					size="large"
				>
					{messages.sections.support.button}
				</Button>
			</Card>
		</section>
	);
}
