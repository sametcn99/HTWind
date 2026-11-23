import { GITHUB_REPOSITORY_URL } from "../../config/constants";
import { useLocale } from "../../i18n/LocaleContext";
import { type LocaleCode, SUPPORTED_LOCALES } from "../../i18n/locales";

export function Banner() {
	const { locale, messages, changeLocale } = useLocale();
	return (
		<header className="site-header wrap">
			<a className="wordmark" href="#top" aria-label="HTWind">
				<img src="/favicon-32x32.png" width="27" height="27" alt="" />
				HTWind<span aria-hidden="true">.</span>
			</a>
			<nav className="site-nav" aria-label={messages.design.navExplore}>
				<a href="#explore">{messages.design.navExplore}</a>
				<a href="#build">{messages.design.navBuild}</a>
				<a href="#questions">{messages.design.navFaq}</a>
			</nav>
			<div className="header-tools">
				<a
					className="source-link"
					href={GITHUB_REPOSITORY_URL}
					target="_blank"
					rel="noopener noreferrer"
				>
					GitHub <span aria-hidden="true">↗</span>
				</a>
				<select
					className="language-select"
					value={locale}
					aria-label={messages.languageSelector.ariaLabel}
					onChange={(event) => changeLocale(event.target.value as LocaleCode)}
				>
					{SUPPORTED_LOCALES.map((code) => (
						<option key={code} value={code}>
							{messages.languageSelector.options[code]}
						</option>
					))}
				</select>
			</div>
		</header>
	);
}
