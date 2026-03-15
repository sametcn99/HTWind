import { Body1, Card, Link, Subtitle1 } from "@fluentui/react-components";
import {
	WIDGET_PACKAGE_EXAMPLE_URL,
	WIDGET_PACKAGE_SCHEMA_URL,
} from "../../config/constants";
import { useLocale } from "../../i18n/LocaleContext";
import { useAppStyles } from "../../styles/appStyles";

export function WidgetPackagesSection() {
	const styles = useAppStyles();
	const { messages } = useLocale();

	return (
		<section aria-labelledby="htwind-widget-packages-heading">
			<Card className={styles.longFormSection}>
				<h2
					id="htwind-widget-packages-heading"
					className={styles.sectionHeading}
				>
					{messages.sections.packages.heading}
				</h2>
				{messages.content.widgetPackageParagraphs.map((paragraph) => (
					<p key={paragraph} className={styles.sectionLead}>
						{paragraph}
					</p>
				))}

				<div className={styles.twoColumnContentGrid}>
					{messages.content.widgetPackageCapabilities.map((item) => (
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
						{messages.sections.packages.flowHeading}
					</Subtitle1>
					<ul className={styles.bulletList}>
						{messages.content.widgetPackageSteps.map((step) => (
							<li key={step}>{step}</li>
						))}
					</ul>
				</div>

				<Body1 className={styles.featureDescription}>
					{messages.sections.packages.schemaPrefix}{" "}
					<Link
						href={WIDGET_PACKAGE_SCHEMA_URL}
						target="_blank"
						rel="noreferrer"
					>
						{messages.sections.packages.schemaLabel}
					</Link>{" "}
					{messages.sections.packages.schemaConnector}{" "}
					<Link
						href={WIDGET_PACKAGE_EXAMPLE_URL}
						target="_blank"
						rel="noreferrer"
					>
						{messages.sections.packages.exampleLabel}
					</Link>{" "}
					{messages.sections.packages.schemaSuffix}
				</Body1>
			</Card>
		</section>
	);
}
