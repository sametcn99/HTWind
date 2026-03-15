export type GitHubReleaseAsset = {
	id: number;
	name: string;
	size: number;
	browser_download_url: string;
};

export type GitHubRelease = {
	tag_name: string;
	name: string;
	published_at: string;
	html_url: string;
	assets?: GitHubReleaseAsset[];
};

export type CachedReleaseRecord = {
	savedAt: number;
	release: GitHubRelease;
};

export type HighlightItem = {
	value: string;
	label: string;
	spanTwo?: boolean;
	spanFour?: boolean;
};

export type FeatureCardItem = {
	title: string;
	description: string;
	icon: "html" | "powershell" | "windows";
};

export type FaqItem = {
	question: string;
	answer: string;
	spanTwoColumns?: boolean;
};

export type ScreenshotItem = {
	src: string;
	alt: string;
};

export type WidgetPackageCapability = {
	title: string;
	description: string;
};
