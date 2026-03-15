import { Body1, Card, Link } from "@fluentui/react-components";
import { SUPPORT_URL } from "../../config/constants";
import { useAppStyles } from "../../styles/appStyles";

export function SupportSection() {
	const styles = useAppStyles();

	return (
		<section aria-labelledby="support-developer-heading">
			<Card className={styles.longFormSection}>
				<h2 id="support-developer-heading" className={styles.sectionHeading}>
					Support the developer
				</h2>
				<p className={styles.sectionLead}>
					You can support sustainable development of HTWind and help the project
					continue to grow by visiting the support page.
				</p>
				<Body1>
					Support link:{" "}
					<Link href={SUPPORT_URL} target="_blank" rel="noreferrer">
						sametcc.me/support
					</Link>
				</Body1>
			</Card>
		</section>
	);
}
