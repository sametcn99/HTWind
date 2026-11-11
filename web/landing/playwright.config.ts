import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
	testDir: "./e2e",
	timeout: 30_000,
	expect: { timeout: 5_000 },
	fullyParallel: true,
	reporter: [["list"]],
	use: {
		baseURL: "http://127.0.0.1:4173",
		...devices["Desktop Chrome"],
	},
	webServer: {
		command:
			"bun run build && bunx vite preview --host 127.0.0.1 --port 4173 --strictPort",
		url: "http://127.0.0.1:4173/",
		reuseExistingServer: true,
		timeout: 120_000,
	},
	projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
