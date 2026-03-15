import { Card, mergeClasses } from "@fluentui/react-components";
import { overviewParagraphs, overviewScreenshots } from "../../config/content";
import type { ScreenshotItem } from "../../config/types";
import { useAppStyles } from "../../styles/appStyles";

type OverviewSectionProps = {
	onSelectImage: (image: ScreenshotItem) => void;
};

export function OverviewSection({ onSelectImage }: OverviewSectionProps) {
	const styles = useAppStyles();

	return (
		<section aria-labelledby="htwind-overview-heading">
			<Card className={styles.longFormSection}>
				<h2 id="htwind-overview-heading" className={styles.sectionHeading}>
					Desktop widget manager for Windows productivity
				</h2>
				{overviewParagraphs.map((paragraph) => (
					<p key={paragraph} className={styles.sectionLead}>
						{paragraph}
					</p>
				))}

				<div className={styles.screenshotsGrid}>
					{overviewScreenshots.map((screenshot) => (
						<button
							type="button"
							key={screenshot.src}
							className={mergeClasses(
								styles.screenshotButton,
								styles.screenshotCard,
								styles.clickableScreenshot,
							)}
							onClick={() => onSelectImage(screenshot)}
							aria-label={`View larger ${screenshot.alt}`}
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
