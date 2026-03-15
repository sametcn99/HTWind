import { Body1, Card, Subtitle1 } from "@fluentui/react-components";
import {
	Desktop24Regular,
	Rocket24Regular,
	WindowWrench24Regular,
} from "@fluentui/react-icons";
import type { FeatureCardItem } from "../../config/types";
import { useLocale } from "../../i18n/LocaleContext";
import { useAppStyles } from "../../styles/appStyles";

function renderIcon(icon: FeatureCardItem["icon"]) {
	switch (icon) {
		case "html":
			return <WindowWrench24Regular fontSize={24} />;
		case "powershell":
			return <Rocket24Regular fontSize={24} />;
		case "windows":
			return <Desktop24Regular fontSize={24} />;
	}
}

export function FeatureCardsSection() {
	const styles = useAppStyles();
	const { messages } = useLocale();

	return (
		<section aria-labelledby="htwind-features-heading">
			<Card className={styles.longFormSection}>
				<h2 id="htwind-features-heading" className={styles.sectionHeading}>
					{messages.sections.features.heading}
				</h2>
				<p className={styles.sectionLead}>{messages.sections.features.lead}</p>
				<div className={styles.contentGrid}>
					{messages.content.featureCards.map((item, index) => (
						<Card
							key={item.title}
							className={
								index === 2 ? styles.featureCardSpanThree : styles.card
							}
						>
							<div className={styles.cardIcon}>
								{renderIcon(item.icon as FeatureCardItem["icon"])}
							</div>
							<Subtitle1 className={styles.featureTitle}>
								{item.title}
							</Subtitle1>
							<Body1 className={styles.featureDescription}>
								{item.description}
							</Body1>
						</Card>
					))}
				</div>
			</Card>
		</section>
	);
}
