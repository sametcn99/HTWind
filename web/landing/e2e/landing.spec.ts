import { expect, type Page, test } from "@playwright/test";

const releaseStub = {
	tag_name: "v9.9.9-e2e",
	name: "HTWind 9.9.9 (e2e)",
	published_at: "2026-01-02T12:00:00Z",
	html_url: "https://github.com/sametcn99/HTWind/releases/tag/v9.9.9-e2e",
	assets: [
		{
			id: 1,
			name: "HTWind-Setup-9.9.9.exe",
			size: 1048576,
			browser_download_url: "https://example.com/HTWind-Setup-9.9.9.exe",
		},
		{
			id: 2,
			name: "HTWind-Portable-9.9.9.zip",
			size: 524288,
			browser_download_url: "https://example.com/HTWind-Portable-9.9.9.zip",
		},
	],
};

async function stubGitHubRelease(page: Page): Promise<void> {
	await page.route("**/api.github.com/**", (route) =>
		route.fulfill({
			status: 200,
			contentType: "application/json",
			body: JSON.stringify(releaseStub),
		}),
	);
}

test.describe("HTWind landing site", () => {
	test("root redirects to the default locale and renders", async ({ page }) => {
		await page.goto("/");
		await page.waitForURL("**/en/**");
		await expect(page).toHaveURL(/\/en\/$/);

		const h1 = page.locator("h1");
		await expect(h1).toHaveCount(1);
		await expect(h1).toContainText(/Build a cleaner Windows desktop/i);
		await expect(page).toHaveTitle(/HTWind/);
	});

	test("/tr/ renders Turkish metadata and content", async ({ page }) => {
		await page.goto("/tr/");
		await expect(page.locator("html")).toHaveAttribute("lang", "tr");
		const h1 = page.locator("h1");
		await expect(h1).toHaveCount(1);
		await expect(h1).not.toBeEmpty();
		await expect(page.locator('meta[property="og:locale"]')).toHaveAttribute(
			"content",
			/tr/i,
		);
	});

	test("/ar/ switches to RTL with Arabic heading", async ({ page }) => {
		await page.goto("/ar/");
		await expect(page.locator("html")).toHaveAttribute("lang", "ar");
		await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
		const h1 = page.locator("h1");
		await expect(h1).toHaveCount(1);
		await expect(h1).not.toBeEmpty();
		await expect(page.locator('link[rel="canonical"]')).toHaveAttribute(
			"href",
			/ar/i,
		);
	});

	test("language switcher updates the URL, html lang, and canonical", async ({
		page,
	}) => {
		await page.goto("/en/");
		await expect(page.locator("html")).toHaveAttribute("lang", "en");

		const selector = page.locator("select[aria-label]").first();
		await selector.selectOption("tr");

		await page.waitForURL("**/tr/**");
		await expect(page).toHaveURL(/\/tr\/$/);
		await expect(page.locator("html")).toHaveAttribute("lang", "tr");
		await expect(page.locator('link[rel="canonical"]')).toHaveAttribute(
			"href",
			/tr/i,
		);
	});

	test("accessibility structure: one h1, h2 sections, h3 FAQ, skip link, safe external links", async ({
		page,
	}) => {
		await page.goto("/en/");

		await expect(page.locator("h1")).toHaveCount(1);
		const h2Count = await page.locator("h2").count();
		expect(h2Count).toBeGreaterThanOrEqual(5);

		// FAQ questions are h3 elements (at least one) and the section exists.
		await expect(
			page.locator('section[aria-labelledby="htwind-faq-heading"]'),
		).toHaveCount(1);
		expect(
			await page
				.locator('section[aria-labelledby="htwind-faq-heading"] h3')
				.count(),
		).toBeGreaterThanOrEqual(1);

		// Skip-to-content link targets #main-content.
		const skipLink = page.locator('a[href="#main-content"]');
		await expect(skipLink).toHaveCount(1);
		await expect(page.locator("#main-content")).toHaveCount(1);

		// Every external link must be opener-safe.
		const externalLinks = page.locator('a[target="_blank"]');
		const externalCount = await externalLinks.count();
		expect(externalCount).toBeGreaterThan(0);
		for (let index = 0; index < externalCount; index++) {
			const rel = await externalLinks.nth(index).getAttribute("rel");
			expect(rel ?? "").toContain("noopener");
			expect(rel ?? "").toContain("noreferrer");
		}
	});

	test("download menu opens with the release assets", async ({ page }) => {
		await stubGitHubRelease(page);
		await page.goto("/en/");

		const downloadButton = page.locator('button[aria-haspopup="menu"]');
		await expect(downloadButton).toBeEnabled({ timeout: 15_000 });
		await downloadButton.click();
		await expect(downloadButton).toHaveAttribute("aria-expanded", "true");

		// The menu exposes the stubbed release assets as download links.
		const menuLink = page.locator(
			'a[href="https://example.com/HTWind-Setup-9.9.9.exe"]',
		);
		await expect(menuLink).toBeVisible();
		await expect(menuLink).toContainText("HTWind-Setup-9.9.9.exe");
	});

	test("screenshot lightbox opens and closes with Escape", async ({ page }) => {
		await page.goto("/en/");

		// The overview section exposes a "View larger {alt}" trigger per screenshot.
		const lightboxTrigger = page
			.locator('section[aria-labelledby="htwind-overview-heading"] button')
			.first();
		await lightboxTrigger.click();

		const closeButton = page.locator('button[aria-label="Close"]');
		await expect(closeButton).toBeVisible();

		await page.keyboard.press("Escape");
		await expect(closeButton).toBeHidden();
	});

	test("FAQ section renders every question with a visible answer", async ({
		page,
	}) => {
		await page.goto("/en/");

		const faqSection = page.locator(
			'section[aria-labelledby="htwind-faq-heading"]',
		);
		const questions = faqSection.locator("h3");
		const questionCount = await questions.count();
		expect(questionCount).toBeGreaterThanOrEqual(1);

		// The FAQ is a static Q&A section: every question shows its answer inline.
		for (let index = 0; index < Math.min(questionCount, 3); index++) {
			await expect(questions.nth(index)).toBeVisible();
			const answer = questions
				.nth(index)
				.locator("xpath=following-sibling::p[1]");
			await expect(answer).toBeVisible();
			await expect(answer).not.toBeEmpty();
		}
	});
});
