import { Card, mergeClasses } from "@fluentui/react-components";
import type { ScreenshotItem } from "../../config/types";
import { interpolate } from "../../i18n/interpolate";
import { useLocale } from "../../i18n/LocaleContext";
import { useAppStyles } from "../../styles/appStyles";

type OverviewSectionProps = {
	onSelectImage: (image: ScreenshotItem) => void;
};

export function OverviewSection({ onSelectImage }: OverviewSectionProps) {
	const styles = useAppStyles();
	const { messages } = useLocale();

	return (
		<section aria-labelledby="htwind-overview-heading">
			<Card className={styles.longFormSection}>
				<h2 id="htwind-overview-heading" className={styles.sectionHeading}>
					{messages.sections.overview.heading}
				</h2>
				{messages.content.overviewParagraphs.map((paragraph) => (
					<p key={paragraph} className={styles.sectionLead}>
						{paragraph}
					</p>
				))}
				<p className={styles.screenshotMeta}>
					{messages.sections.overview.screenshotHint}
				</p>

				<div className={styles.screenshotsGrid}>
					{messages.content.overviewScreenshots.map((screenshot) => (
						<button
							type="button"
							key={screenshot.src}
							className={mergeClasses(
								styles.screenshotButton,
								styles.screenshotCard,
								styles.clickableScreenshot,
							)}
							onClick={() => onSelectImage(screenshot)}
							aria-label={interpolate(messages.sections.overview.viewLarger, {
								alt: screenshot.alt,
							})}
						>
							<img
								src={screenshot.src}
								alt={screenshot.alt}
								className={styles.screenshotImage}
								loading="lazy"
								decoding="async"
							/>
						</button>
					))}
				</div>
			</Card>
		</section>
	);
}
