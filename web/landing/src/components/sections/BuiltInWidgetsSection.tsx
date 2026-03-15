import { Badge, Body1, Card, Link, Title1 } from "@fluentui/react-components";
import {
	GITHUB_REPOSITORY_URL,
	WIDGET_PROMPT_SHARED_URL,
	WIDGET_PROMPT_URL,
} from "../../config/constants";
import { widgets } from "../../config/content";
import { useAppStyles } from "../../styles/appStyles";

export function BuiltInWidgetsSection() {
	const styles = useAppStyles();

	return (
		<section>
			<Card className={styles.widgetsCard}>
				<Title1>Built-in widgets</Title1>
				<Body1 className={styles.featureDescription}>
					HTWind ships with practical templates for system insight, media
					controls, file operations, and quick actions. You can also package
					custom widgets with local assets or group multiple widgets together
					through the manifest-based package format.
				</Body1>
				<div className={styles.widgetList}>
					{widgets.map((widget) => (
						<Badge
							key={widget}
							size="large"
							appearance="tint"
							color="informative"
						>
							{widget}
						</Badge>
					))}
				</div>
				<Body1>
					For complete details, check the{" "}
					<Link
						href={`${GITHUB_REPOSITORY_URL}#built-in-widgets`}
						target="_blank"
						rel="noreferrer"
					>
						repository documentation
					</Link>
					.
				</Body1>
				<Body1 className={styles.featureDescription}>
					Want to generate HTWind widgets with LLM assistance? Use the{" "}
					<Link href={WIDGET_PROMPT_URL} target="_blank" rel="noreferrer">
						HTWind Widget Generator Prompt
					</Link>{" "}
					or the{" "}
					<Link
						href={WIDGET_PROMPT_SHARED_URL}
						target="_blank"
						rel="noreferrer"
					>
						shared prompts.chat version
					</Link>{" "}
					to produce HTWind-compatible single-file widgets first, then evolve
					them into asset-backed or multi-widget packages when the project
					grows.
				</Body1>
			</Card>
		</section>
	);
}
