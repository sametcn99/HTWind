import type {
	FaqItem,
	FeatureCardItem,
	HighlightItem,
	ScreenshotItem,
	WidgetPackageCapability,
} from "./types";

export const widgets = [
	"clock",
	"weather",
	"network-tools",
	"process-manager",
	"clipboard-studio",
	"file-explorer",
	"dns-lookup",
	"powershell-console",
];

export const highlights: HighlightItem[] = [
	{ value: "20+", label: "Built-in templates" },
	{ value: "Hot Reload", label: "Live widget preview while editing" },
	{ value: "WebView2", label: "Native rendering stack" },
	{ value: "Open Source", label: "GPL 3.0 licensed project" },
	{
		value: "Smart Suppression",
		label: "Fullscreen and maximized display-aware hide modes",
		spanTwo: true,
	},
	{
		value: "Built-in Editor",
		label: "Create and edit widgets in-app",
		spanTwo: true,
	},
	{
		value: "Manifest Packages",
		label:
			"Ship asset folders or multiple widgets through one package manifest",
		spanTwo: true,
	},
	{
		value: "PowerShell API",
		label: "Host-side automation bridge",
		spanTwo: true,
	},
];

export const featureCards: FeatureCardItem[] = [
	{
		title: "HTML Widgets on Desktop",
		description:
			"Build and run widgets with HTML, CSS, and JavaScript. HTWind supports single-file widgets, asset-backed widget folders, and manifest-driven widget packages.",
		icon: "html",
	},
	{
		title: "PowerShell Integration",
		description:
			"Trigger safe, explicit PowerShell scripts through HTWind host APIs for quick diagnostics, automation, and system actions.",
		icon: "powershell",
	},
	{
		title: "Windows 11 Native Feel",
		description:
			"Designed for modern Windows workflows with tray behavior, pin-on-top controls, and state persistence tuned for desktop productivity, including display-aware suppression options for fullscreen and maximized foreground apps.",
		icon: "windows",
	},
];

export const workflowUseCases = [
	"Create a always-on desktop clock, weather panel, and quick actions bar for daily focus sessions.",
	"Run network checks, DNS lookups, and process monitoring widgets during development or support tasks.",
	"Use HTWind PowerShell integration for controlled automation while keeping execution consent explicit.",
	"Build custom internal widgets with HTML, CSS, and JavaScript to expose team-specific operational tools.",
	"Bundle dashboards, timers, status boards, and their CSS or JavaScript assets into one manifest package so teams can import a complete widget set in one step.",
];

export const widgetPackageParagraphs = [
	"HTWind now supports manifest-based widget packages through htwind.widget.json. This means a widget is no longer limited to a single HTML file. You can ship full widget folders with CSS, JavaScript, images, and additional assets, then describe the package through a single manifest file.",
	"The same package format also supports multiple widgets in one import. A single manifest can describe several widget folders, each with its own entry file and declared assets. This is useful for curated widget bundles, internal toolkits, and reusable desktop setups that should be installed together.",
	"The import and export workflow is designed for both creators and teams. You can import a standalone HTML widget, import a manifest package, export a single managed widget as a package, or export the current workspace as a package-oriented bundle for sharing and backup.",
];

export const widgetPackageCapabilities: WidgetPackageCapability[] = [
	{
		title: "Asset-backed widgets",
		description:
			"Package widgets that depend on CSS, JavaScript, images, fonts, or other local assets instead of flattening everything into one file.",
	},
	{
		title: "Multiple widgets per package",
		description:
			"Describe several widget folders inside one manifest so HTWind can import a complete dashboard set or toolkit in one action.",
	},
	{
		title: "Schema-guided structure",
		description:
			"Use the included schema and example package as a reference for consistent relative paths, entry files, and asset declarations.",
	},
	{
		title: "Import and export ready",
		description:
			"Move from development to sharing without repackaging by hand: import manifests directly, export one widget, or export the entire workspace.",
	},
];

export const widgetPackageSteps = [
	"Create a package root with htwind.widget.json at the top level.",
	"Add one or more widget folders and point each widget entry to its HTML file through relative paths.",
	"List the non-entry assets for each widget so the package stays explicit and portable.",
	"Import the manifest into HTWind, or export an existing widget/workspace to generate a reusable package layout.",
];

export const communitySharingSteps = [
	"Share widget releases in GitHub Discussions and post highlights in the HTWind Reddit community to reach more users.",
	"Add a short widget summary, screenshots, and the main Windows workflow your widget improves.",
	"Include setup notes, expected permissions, and any host API or PowerShell usage details for safe adoption.",
	"Publish update notes in both channels so users can track versions, fixes, and feature changes.",
];

export const faqItems: FaqItem[] = [
	{
		question: "What is HTWind used for?",
		answer:
			"HTWind is used to manage and display HTML-based desktop widgets on Windows. It helps users keep diagnostics, productivity shortcuts, and system insights visible on top of regular applications.",
	},
	{
		question: "Can I build my own widgets?",
		answer:
			"Yes. You can create custom widgets with web technologies including HTML, CSS, and JavaScript. HTWind supports single-file widgets, asset-backed widget folders, and manifest packages that can describe one or many widgets.",
	},
	{
		question: "What is htwind.widget.json used for?",
		answer:
			"htwind.widget.json is the widget package manifest. It defines package metadata and one or more widgets, including each widget's relative folder, entry HTML file, and declared assets. Use it when you want to import or share widgets that contain multiple files or multiple widgets in one package.",
		spanTwoColumns: true,
	},
	{
		question: "Does HTWind support PowerShell automation?",
		answer:
			"Yes. HTWind includes host-side APIs that can execute approved PowerShell commands, enabling practical automation workflows for diagnostics and controlled desktop actions.",
	},
	{
		question: "Can widgets auto-hide while I use fullscreen or maximized apps?",
		answer:
			"Yes. In Settings, you can enable fullscreen suppression and an optional separate maximized-app suppression mode. Both modes work per display and temporarily close widget windows without changing the saved widget visibility state.",
	},
	{
		question: "Is HTWind open source?",
		answer:
			"HTWind is an open-source project published on GitHub under the GPL 3.0 license, making it suitable for personal use, experimentation, and community-driven improvements.",
	},
	{
		question: "Are contributions welcome?",
		answer:
			"Yes. Contributions are welcome. You can open issues, share ideas in Discussions, and submit pull requests to improve widgets, templates, documentation, and core app features.",
		spanTwoColumns: false,
	},
];

export const overviewParagraphs = [
	"HTWind is a Windows desktop widget manager that combines native app behavior with flexible web content. By using HTML widgets and WebView2 rendering, the platform makes it easier to build desktop tools that stay accessible while you work. Typical setups include clock and calendar utilities, system monitoring dashboards, quick command launchers, and asset-backed widgets that remain visible across multi-window workflows.",
	"The project is designed for users who want a lightweight but extensible desktop customization layer. With tray integration, pin-on-top window controls, state persistence, template-based widgets, and the new manifest package format, HTWind helps turn a standard Windows 11 workspace into a more actionable and information-rich environment without requiring heavy desktop shell replacements. Settings also include display-aware suppression modes that temporarily close widget windows while other apps are fullscreen or maximized.",
];

export const overviewScreenshots: ScreenshotItem[] = [
	{
		src: "/app_screenshots/page_home.png",
		alt: "HTWind Home - Widget Library",
	},
	{
		src: "/app_screenshots/page_settings.png",
		alt: "HTWind Settings - Customization",
	},
	{
		src: "/app_screenshots/page_about.png",
		alt: "HTWind About - Version Info",
	},
];
