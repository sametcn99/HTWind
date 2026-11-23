import { useState } from "react";
import {
	GITHUB_REPOSITORY_URL,
	WIDGET_PACKAGE_EXAMPLE_URL,
	WIDGET_PACKAGE_SCHEMA_URL,
	WIDGET_PROMPT_URL,
} from "../../config/constants";
import type { ScreenshotItem } from "../../config/types";
import { interpolate } from "../../i18n/interpolate";
import { useLocale } from "../../i18n/LocaleContext";

export function ProductFacts() {
	const { messages } = useLocale();
	return (
		<ul
			className="product-facts wrap"
			aria-label={messages.sections.highlights.ariaLabel}
		>
			{messages.content.highlights.map((highlight) => (
				<li key={highlight.value}>
					<strong>{highlight.value}</strong>
					<span>{highlight.label}</span>
				</li>
			))}
			<li>
				<strong>HTML / CSS / JS</strong>
				<span>WebView2</span>
			</li>
		</ul>
	);
}

export function AppShowcase({
	onSelectImage,
}: {
	onSelectImage: (image: ScreenshotItem) => void;
}) {
	const { messages } = useLocale();
	const [active, setActive] = useState(1);
	const labels = [
		messages.design.library,
		messages.design.gallery,
		messages.design.settings,
	];
	const screenshot = messages.content.overviewScreenshots[active];
	return (
		<section
			className="showcase section wrap"
			id="explore"
			aria-labelledby="htwind-overview-heading"
		>
			<div className="section-intro">
				<div>
					<p className="eyebrow">HTWind</p>
					<h2 id="htwind-overview-heading">{messages.design.appHeading}</h2>
				</div>
				<p>{messages.design.appDescription}</p>
			</div>
			<div className="showcase-toolbar">
				<fieldset
					className="segmented-control"
					aria-label={messages.design.navExplore}
				>
					{labels.map((label, index) => (
						<button
							key={label}
							type="button"
							aria-pressed={active === index}
							onClick={() => setActive(index)}
						>
							{label}
						</button>
					))}
				</fieldset>
				<span className="mono">Windows · WebView2</span>
			</div>
			<div className="showcase-stage">
				<button
					className="screenshot-button"
					type="button"
					aria-label={interpolate(messages.sections.overview.viewLarger, {
						alt: screenshot.alt,
					})}
					onClick={() => onSelectImage(screenshot)}
				>
					<img
						key={screenshot.src}
						src={screenshot.src}
						alt={screenshot.alt}
						width="1380"
						height="990"
						loading="lazy"
						decoding="async"
					/>
					<span className="expand-cue" aria-hidden="true">
						↗
					</span>
				</button>
			</div>
			<div className="showcase-caption">
				<span>{screenshot.alt}</span>
				<span>{messages.sections.overview.screenshotHint}</span>
			</div>
		</section>
	);
}

export function WidgetCollection() {
	const { messages } = useLocale();
	return (
		<section
			className="widgets-section section wrap"
			aria-labelledby="widgets-heading"
		>
			<div className="section-intro">
				<h2 id="widgets-heading">{messages.design.widgetsHeading}</h2>
				<a
					className="text-link"
					href={`${GITHUB_REPOSITORY_URL}/tree/main/HTWind/Templates`}
					target="_blank"
					rel="noopener noreferrer"
				>
					{messages.design.source}
					<span aria-hidden="true">↗</span>
				</a>
			</div>
			<div className="widget-shelf">
				{messages.content.widgets.map((widget) => (
					<a
						key={widget.id}
						href={`${GITHUB_REPOSITORY_URL}/blob/main/HTWind/Templates/${widget.id}.html`}
						target="_blank"
						rel="noopener noreferrer"
					>
						<span className="widget-file" aria-hidden="true">
							&lt;/&gt;
						</span>
						<span>{widget.label}</span>
						<span className="widget-extension">
							.html <span aria-hidden="true">↗</span>
						</span>
					</a>
				))}
			</div>
			<div className="capability-list">
				{messages.content.featureCards.map((feature, index) => (
					<article key={feature.title}>
						<span className="capability-symbol" aria-hidden="true">
							{["</>", ">_", "⊞"][index]}
						</span>
						<h3>{feature.title}</h3>
						<p>{feature.description}</p>
					</article>
				))}
			</div>
		</section>
	);
}

const htmlExample = `<style>
  body { color: #eff4ec; font: 32px system-ui; }
</style>
<time id="clock"></time>
<script>
  setInterval(() => {
    document.querySelector("#clock").textContent = new Date().toLocaleTimeString();
  }, 1000);
</script>`;
const manifestExample = JSON.stringify(
	{
		schemaVersion: 1,
		name: "my-desktop",
		widgets: [
			{
				widgetId: "clock-widget",
				name: "clock",
				relativePath: ".",
				entryFile: "clock.html",
				assets: [],
			},
		],
	},
	null,
	2,
);

export function BuilderSection() {
	const { messages } = useLocale();
	const [showManifest, setShowManifest] = useState(false);
	return (
		<section
			id="build"
			className="builder-section section"
			aria-labelledby="build-heading"
		>
			<div className="builder-inner wrap">
				<div className="builder-copy">
					<p className="eyebrow">HTML / CSS / JavaScript</p>
					<h2 id="build-heading">{messages.design.buildHeading}</h2>
					<p>{messages.design.buildDescription}</p>
					<a
						className="button button-light"
						href={GITHUB_REPOSITORY_URL}
						target="_blank"
						rel="noopener noreferrer"
					>
						{messages.design.docs}
						<span aria-hidden="true">↗</span>
					</a>
					<div className="builder-resources">
						<a
							href={WIDGET_PACKAGE_SCHEMA_URL}
							target="_blank"
							rel="noopener noreferrer"
						>
							{messages.sections.packages.schemaLabel} ↗
						</a>
						<a
							href={WIDGET_PACKAGE_EXAMPLE_URL}
							target="_blank"
							rel="noopener noreferrer"
						>
							{messages.sections.packages.exampleLabel} ↗
						</a>
						<a
							href={WIDGET_PROMPT_URL}
							target="_blank"
							rel="noopener noreferrer"
						>
							{messages.sections.widgets.promptLabel} ↗
						</a>
					</div>
				</div>
				<div className="code-panel" dir="ltr">
					<fieldset className="code-tabs" aria-label={messages.design.navBuild}>
						<button
							type="button"
							aria-pressed={!showManifest}
							onClick={() => setShowManifest(false)}
						>
							clock.html
						</button>
						<button
							type="button"
							aria-pressed={showManifest}
							onClick={() => setShowManifest(true)}
						>
							htwind.widget.json
						</button>
					</fieldset>
					<pre>
						<code>{showManifest ? manifestExample : htmlExample}</code>
					</pre>
					<div className="code-status">
						<span className="status-dot" />
						{showManifest ? "JSON" : "HTML"}
						<span>UTF-8</span>
					</div>
				</div>
			</div>
		</section>
	);
}

export function FaqSection() {
	const { messages } = useLocale();
	return (
		<section
			className="faq-section section wrap"
			id="questions"
			aria-labelledby="htwind-faq-heading"
		>
			<div>
				<p className="eyebrow">FAQ</p>
				<h2 id="htwind-faq-heading">{messages.design.faqHeading}</h2>
			</div>
			<div className="faq-list">
				{messages.content.faqItems.map((faq) => (
					<details key={faq.question}>
						<summary>
							<h3>{faq.question}</h3>
							<span aria-hidden="true">+</span>
						</summary>
						<p>{faq.answer}</p>
					</details>
				))}
			</div>
		</section>
	);
}
