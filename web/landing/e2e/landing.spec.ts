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
	test.beforeEach(async ({ page }) => {
		await stubGitHubRelease(page);
	});

	test("release API failure keeps the page usable and falls back to the release feed", async ({
		page,
	}) => {
		await page.route("**/api.github.com/**", (route) =>
			route.fulfill({ status: 503, body: "unavailable" }),
		);
		await page.goto("/en/");

		await expect(
			page.getByText("Release info unavailable", { exact: true }),
		).toBeVisible();
		await expect(page.locator(".download-toggle")).toBeDisabled();
		await expect(page.locator(".button-primary")).toHaveAttribute(
			"href",
			"https://github.com/sametcn99/HTWind/releases",
		);
		await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
	});

	test("malformed release assets cannot crash the page or expose downloads", async ({
		page,
	}) => {
		const pageErrors: string[] = [];
		page.on("pageerror", (error) => pageErrors.push(error.message));
		await page.route("**/api.github.com/**", (route) =>
			route.fulfill({
				status: 200,
				contentType: "application/json",
				body: JSON.stringify({ ...releaseStub, assets: [null] }),
			}),
		);
		await page.goto("/en/");

		await expect(
			page.getByText("Release info unavailable", { exact: true }),
		).toBeVisible();
		await expect(page.locator(".download-toggle")).toBeDisabled();
		await expect(page.locator(".button-primary")).toHaveAttribute(
			"href",
			"https://github.com/sametcn99/HTWind/releases",
		);
		expect(pageErrors).toEqual([]);
	});

	test("cached downloads remain available after reload when GitHub is unavailable", async ({
		page,
	}) => {
		await page.goto("/en/");
		const toggle = page.locator(".download-toggle");
		await expect(toggle).toBeEnabled();
		await expect(page.locator(".button-primary")).toHaveAttribute(
			"href",
			"https://example.com/HTWind-Setup-9.9.9.exe",
		);

		await page.route("**/api.github.com/**", (route) =>
			route.fulfill({ status: 429, body: "rate limited" }),
		);
		const refresh = page.waitForResponse(
			(response) =>
				response.url().includes("api.github.com") && response.status() === 429,
		);
		await page.reload();
		await refresh;
		await expect(toggle).toBeEnabled();
		await toggle.click();
		await expect(
			page.getByRole("link", { name: /HTWind-Setup-9.9.9.exe/ }),
		).toBeVisible();
	});

	test("download disclosure supports keyboard opening, Escape, and outside dismissal", async ({
		page,
	}) => {
		await page.goto("/en/");
		const toggle = page.locator(".download-toggle");
		await expect(toggle).toBeEnabled();
		await toggle.focus();
		await page.keyboard.press("Enter");
		await expect(toggle).toHaveAttribute("aria-expanded", "true");
		await expect(
			page.getByRole("link", { name: /HTWind-Setup-9.9.9.exe/ }),
		).toBeVisible();

		await page.keyboard.press("Escape");
		await expect(toggle).toHaveAttribute("aria-expanded", "false");
		await expect(toggle).toBeFocused();

		await toggle.click();
		await page.getByRole("heading", { level: 1 }).click();
		await expect(toggle).toHaveAttribute("aria-expanded", "false");
	});

	test("primary download links directly to the installer", async ({ page }) => {
		await page.goto("/en/");
		const installerLink = page.locator(".button-primary");
		await expect(installerLink).toHaveAttribute(
			"href",
			"https://example.com/HTWind-Setup-9.9.9.exe",
		);
		await expect(installerLink).toHaveAttribute("target", "_blank");
		await expect(installerLink).toHaveAttribute("rel", /noopener noreferrer/);
	});

	test("root redirects to the default locale and renders", async ({ page }) => {
		await page.goto("/");
		await page.waitForURL("**/en/**");
		await expect(page).toHaveURL(/\/en\/$/);

		const h1 = page.locator("h1");
		await expect(h1).toHaveCount(1);
		await expect(h1).toContainText(/Useful tools\.\s+Right on your desktop\./i);
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

	test("long German, Turkish, and Arabic copy has no horizontal page overflow on mobile and tablet", async ({
		page,
	}) => {
		for (const width of [320, 390, 768]) {
			await page.setViewportSize({ width, height: 844 });
			for (const locale of ["de", "tr", "ar"]) {
				await page.goto(`/${locale}/`);
				const heading = page.locator("h1");
				await expect(heading).toBeVisible();
				if (locale === "ar") {
					await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
				}
				const headingDimensions = await heading.evaluate((element) => ({
					client: element.clientWidth,
					scroll: element.scrollWidth,
				}));
				expect(
					headingDimensions.scroll,
					`${locale} heading at ${width}px should not overflow its container`,
				).toBeLessThanOrEqual(headingDimensions.client);
				const dimensions = await page.evaluate(() => ({
					viewport: document.documentElement.clientWidth,
					content: document.documentElement.scrollWidth,
				}));
				expect(
					dimensions.content,
					`${locale} at ${width}px should not overflow horizontally`,
				).toBeLessThanOrEqual(dimensions.viewport);
			}
		}
	});

	test("accessibility structure has one h1, section headings, skip link, and safe external links", async ({
		page,
	}) => {
		await page.goto("/en/");

		await expect(page.locator("h1")).toHaveCount(1);
		const h2Count = await page.locator("h2").count();
		expect(h2Count).toBeGreaterThanOrEqual(4);

		const faqSection = page.locator(
			'section[aria-labelledby="htwind-faq-heading"]',
		);
		await expect(faqSection).toHaveCount(1);
		await expect(faqSection.locator("h3").first()).toBeVisible();

		const skipLink = page.locator('a[href="#main-content"]');
		await expect(skipLink).toHaveCount(1);
		await expect(page.locator("#main-content")).toHaveCount(1);

		const externalLinks = page.locator('a[target="_blank"]');
		const externalCount = await externalLinks.count();
		expect(externalCount).toBeGreaterThan(0);
		for (let index = 0; index < externalCount; index++) {
			const rel = await externalLinks.nth(index).getAttribute("rel");
			expect(rel ?? "").toContain("noopener");
			expect(rel ?? "").toContain("noreferrer");
		}
	});

	test("gallery selector changes the screenshot and opens a focus-restoring dialog", async ({
		page,
	}) => {
		await page.goto("/en/");
		const gallery = page.locator("#explore");
		const gallerySelector = gallery.locator(".segmented-control");
		await expect(gallerySelector.getByRole("button")).toHaveCount(3);
		const screenshots = [
			{ label: "Library", alt: "HTWind Home - Widget Library" },
			{ label: "Gallery", alt: "HTWind Gallery - Install community widgets" },
			{ label: "Settings", alt: "HTWind Settings - Customization" },
		];
		for (const screenshot of screenshots) {
			const option = gallerySelector.getByRole("button", {
				name: screenshot.label,
			});
			await option.click();
			await expect(option).toHaveAttribute("aria-pressed", "true");
			await expect(gallery.locator(".screenshot-button img")).toHaveAttribute(
				"alt",
				screenshot.alt,
			);
			const displayedImage = gallery.locator(".screenshot-button img");
			await expect
				.poll(() =>
					displayedImage.evaluate(
						(image: HTMLImageElement) =>
							image.complete && image.naturalWidth > 0,
					),
				)
				.toBe(true);
		}
		const galleryButton = gallerySelector.getByRole("button", {
			name: "Gallery",
		});
		await galleryButton.click();
		const screenshotTrigger = gallery.locator(".screenshot-button");

		await screenshotTrigger.click();
		const dialog = page.locator("dialog.screenshot-dialog");
		await expect(dialog).toBeVisible();
		await expect(dialog).toHaveAttribute(
			"aria-label",
			"HTWind Gallery - Install community widgets",
		);
		await page.keyboard.press("Escape");
		await expect(dialog).toBeHidden();
		await expect(screenshotTrigger).toBeFocused();
	});

	test("code tabs switch between the HTML example and widget manifest", async ({
		page,
	}) => {
		await page.goto("/en/");
		const codeTabs = page.locator(".code-tabs");
		const htmlTab = codeTabs.getByRole("button", { name: "clock.html" });
		const manifestTab = codeTabs.getByRole("button", {
			name: "htwind.widget.json",
		});
		await expect(htmlTab).toHaveAttribute("aria-pressed", "true");
		await expect(page.locator(".code-panel code")).toContainText("setInterval");

		await manifestTab.click();
		await expect(manifestTab).toHaveAttribute("aria-pressed", "true");
		await expect(page.locator(".code-panel code")).toContainText(
			'"schemaVersion": 1',
		);
		await expect(page.locator(".code-status")).toContainText("JSON");

		await htmlTab.click();
		await expect(htmlTab).toHaveAttribute("aria-pressed", "true");
		await expect(page.locator(".code-status")).toContainText("HTML");
	});

	test("FAQ keeps nine native disclosures that keyboard users can expand", async ({
		page,
	}) => {
		await page.goto("/en/");
		const faqSection = page.locator(
			'section[aria-labelledby="htwind-faq-heading"]',
		);
		const items = faqSection.locator("details");
		await expect(items).toHaveCount(9);

		const firstItem = items.first();
		const summary = firstItem.locator("summary");
		await summary.focus();
		await page.keyboard.press("Enter");
		await expect(firstItem).toHaveAttribute("open", "");
		await expect(firstItem.locator("p")).toBeVisible();
		await expect(firstItem.locator("p")).not.toBeEmpty();
	});
});
