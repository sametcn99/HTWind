import { Body1, Card, Link, Subtitle1 } from "@fluentui/react-components";
import {
	WIDGET_PACKAGE_EXAMPLE_URL,
	WIDGET_PACKAGE_SCHEMA_URL,
} from "../../config/constants";
import {
	widgetPackageCapabilities,
	widgetPackageParagraphs,
	widgetPackageSteps,
} from "../../config/content";
import { useAppStyles } from "../../styles/appStyles";

export function WidgetPackagesSection() {
	const styles = useAppStyles();

	return (
		<section aria-labelledby="htwind-widget-packages-heading">
			<Card className={styles.longFormSection}>
				<h2
					id="htwind-widget-packages-heading"
					className={styles.sectionHeading}
				>
					Manifest packages for multiple widgets and local assets
				</h2>
				{widgetPackageParagraphs.map((paragraph) => (
					<p key={paragraph} className={styles.sectionLead}>
						{paragraph}
					</p>
				))}

				<div className={styles.twoColumnContentGrid}>
					{widgetPackageCapabilities.map((item) => (
						<Card key={item.title} className={styles.card}>
							<Subtitle1 className={styles.featureTitle}>
								{item.title}
							</Subtitle1>
							<Body1 className={styles.featureDescription}>
								{item.description}
							</Body1>
						</Card>
					))}
				</div>

				<div>
					<Subtitle1 className={styles.featureTitle}>
						Typical package flow
					</Subtitle1>
					<ul className={styles.bulletList}>
						{widgetPackageSteps.map((step) => (
							<li key={step}>{step}</li>
						))}
					</ul>
				</div>

				<Body1 className={styles.featureDescription}>
					Start from the{" "}
					<Link
						href={WIDGET_PACKAGE_SCHEMA_URL}
						target="_blank"
						rel="noreferrer"
					>
						schema file
					</Link>{" "}
					for the manifest contract, or inspect the{" "}
					<Link
						href={WIDGET_PACKAGE_EXAMPLE_URL}
						target="_blank"
						rel="noreferrer"
					>
						multi-widget example package
					</Link>{" "}
					to see how HTWind declares multiple widget folders in a single
					importable bundle.
				</Body1>
			</Card>
		</section>
	);
}
