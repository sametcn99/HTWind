import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

// https://vite.dev/config/
export default defineConfig({
	plugins: [react()],
	build: {
		rollupOptions: {
			output: {
				manualChunks(id) {
					if (!id.includes("node_modules")) {
						return;
					}

					if (id.includes("@fluentui")) {
						return "fluentui";
					}

					if (
						id.includes("react-zoom-pan-pinch") ||
						id.includes("use-sync-external-store")
					) {
						return "media";
					}

					if (id.includes("i18next")) {
						return "i18n";
					}

					if (
						id.includes("react") ||
						id.includes("react-dom") ||
						id.includes("scheduler")
					) {
						return "react-vendor";
					}

					return "vendor";
				},
			},
		},
	},
});
