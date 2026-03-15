import { FluentProvider, webDarkTheme } from "@fluentui/react-components";
import { useState } from "react";
import "./App.css";
import { HeroSection } from "./components/hero/HeroSection";
import { Banner } from "./components/layout/Banner";
import { Footer } from "./components/layout/Footer";
import { ScreenshotLightbox } from "./components/media/ScreenshotLightbox";
import { BuiltInWidgetsSection } from "./components/sections/BuiltInWidgetsSection";
import { CommunitySection } from "./components/sections/CommunitySection";
import { FaqSection } from "./components/sections/FaqSection";
import { FeatureCardsSection } from "./components/sections/FeatureCardsSection";
import { HighlightsSection } from "./components/sections/HighlightsSection";
import { OverviewSection } from "./components/sections/OverviewSection";
import { SupportSection } from "./components/sections/SupportSection";
import { UseCasesSection } from "./components/sections/UseCasesSection";
import { WidgetPackagesSection } from "./components/sections/WidgetPackagesSection";
import type { ScreenshotItem } from "./config/types";
import { useLatestRelease } from "./hooks/useLatestRelease";
import { useAppStyles } from "./styles/appStyles";

function App() {
	const styles = useAppStyles();
	const [selectedImage, setSelectedImage] = useState<ScreenshotItem | null>(
		null,
	);
	const {
		latestRelease,
		isLoadingRelease,
		releaseText,
		releaseDate,
		latestAssets,
	} = useLatestRelease();

	return (
		<FluentProvider theme={webDarkTheme} className={styles.page}>
			<main className={styles.shell}>
				<Banner />
				<HeroSection
					isLoadingRelease={isLoadingRelease}
					releaseText={releaseText}
					releaseDate={releaseDate}
					latestReleaseUrl={latestRelease?.html_url}
					latestAssets={latestAssets}
				/>
				<HighlightsSection />
				<FeatureCardsSection />
				<BuiltInWidgetsSection />
				<WidgetPackagesSection />
				<OverviewSection onSelectImage={setSelectedImage} />
				<UseCasesSection />
				<FaqSection />
				<CommunitySection />
				<SupportSection />
				<Footer />
			</main>
			<ScreenshotLightbox
				selectedImage={selectedImage}
				onClose={() => setSelectedImage(null)}
			/>
		</FluentProvider>
	);
}

export default App;
