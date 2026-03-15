import { FluentProvider, webDarkTheme } from "@fluentui/react-components";
import { useEffect, useState } from "react";
import i18n from "./i18n/setup";
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
import { useHeadMetadata } from "./hooks/useHeadMetadata";
import { useLatestRelease } from "./hooks/useLatestRelease";
import { LocaleProvider } from "./i18n/LocaleContext";
import {
	buildLocalizedPath,
	DEFAULT_LOCALE,
	getLocaleFromPath,
	type LocaleCode,
	normalizeLocale,
} from "./i18n/locales";
import { getLocaleMessages } from "./i18n/resources";
import { readSavedLocale, writeSavedLocale } from "./lib/storage";
import { useAppStyles } from "./styles/appStyles";

function getResolvedLocale(): LocaleCode {
	return (
		normalizeLocale(i18n.resolvedLanguage ?? i18n.language) ?? DEFAULT_LOCALE
	);
}

function App() {
	const styles = useAppStyles();
	const [locale, setLocale] = useState<LocaleCode>(() => getResolvedLocale());
	const messages = getLocaleMessages(locale);
	const [selectedImage, setSelectedImage] = useState<ScreenshotItem | null>(
		null,
	);
	const {
		latestRelease,
		isLoadingRelease,
		releaseText,
		releaseDate,
		latestAssets,
	} = useLatestRelease(locale, messages);

	useHeadMetadata(locale, messages);

	useEffect(() => {
		void i18n.changeLanguage(locale);
		writeSavedLocale(locale);

		const currentPathLocale = getLocaleFromPath(window.location.pathname);
		if (currentPathLocale === locale) {
			return;
		}

		const targetPath = buildLocalizedPath(
			locale,
			window.location.pathname,
			window.location.search,
			window.location.hash,
		);

		window.history.replaceState(null, "", targetPath);
	}, [locale]);

	useEffect(() => {
		function handlePopState() {
			const pathLocale = getLocaleFromPath(window.location.pathname);
			const nextLocale = pathLocale ?? readSavedLocale() ?? getResolvedLocale();

			setLocale((currentLocale) =>
				currentLocale === nextLocale ? currentLocale : nextLocale,
			);
		}

		window.addEventListener("popstate", handlePopState);
		return () => {
			window.removeEventListener("popstate", handlePopState);
		};
	}, []);

	function changeLocale(nextLocale: LocaleCode) {
		if (nextLocale === locale) {
			return;
		}

		void i18n.changeLanguage(nextLocale);
		writeSavedLocale(nextLocale);
		const targetPath = buildLocalizedPath(
			nextLocale,
			window.location.pathname,
			window.location.search,
			window.location.hash,
		);

		window.history.pushState(null, "", targetPath);
		setLocale(nextLocale);
	}

	return (
		<LocaleProvider locale={locale} changeLocale={changeLocale}>
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
					<OverviewSection onSelectImage={setSelectedImage} />
					<FeatureCardsSection />
					<BuiltInWidgetsSection />
					<WidgetPackagesSection />
					<UseCasesSection />
					<CommunitySection />
					<FaqSection />
					<SupportSection />
					<Footer />
				</main>
				<ScreenshotLightbox
					selectedImage={selectedImage}
					onClose={() => setSelectedImage(null)}
				/>
			</FluentProvider>
		</LocaleProvider>
	);
}

export default App;
