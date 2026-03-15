import { Body1, Card, Subtitle1 } from "@fluentui/react-components";
import {
	Desktop24Regular,
	Rocket24Regular,
	WindowWrench24Regular,
} from "@fluentui/react-icons";
import { featureCards } from "../../config/content";
import { useAppStyles } from "../../styles/appStyles";

function renderIcon(icon: (typeof featureCards)[number]["icon"]) {
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

	return (
		<section className={styles.contentGrid}>
			{featureCards.map((item) => (
				<Card key={item.title} className={styles.card}>
					{renderIcon(item.icon)}
					<Subtitle1 className={styles.featureTitle}>{item.title}</Subtitle1>
					<Body1 className={styles.featureDescription}>
						{item.description}
					</Body1>
				</Card>
			))}
		</section>
	);
}
