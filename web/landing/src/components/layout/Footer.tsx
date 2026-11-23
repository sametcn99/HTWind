import {
	GITHUB_DISCUSSIONS_URL,
	GITHUB_REPOSITORY_URL,
	MICROSOFT_STORE_URL,
	REDDIT_COMMUNITY_URL,
	RELEASES_URL,
	SUPPORT_URL,
} from "../../config/constants";
import { useLocale } from "../../i18n/LocaleContext";

export function Footer() {
	const { messages } = useLocale();
	return (
		<footer className="site-footer">
			<div className="wrap">
				<div className="footer-top">
					<div>
						<h2>{messages.design.communityHeading}</h2>
						<p>{messages.design.communityDescription}</p>
					</div>
					<a
						className="button button-outline"
						href={GITHUB_DISCUSSIONS_URL}
						target="_blank"
						rel="noopener noreferrer"
					>
						{messages.footer.discussions}
						<span aria-hidden="true">↗</span>
					</a>
				</div>
				<div className="footer-links">
					<a
						href={GITHUB_REPOSITORY_URL}
						target="_blank"
						rel="noopener noreferrer"
					>
						GitHub ↗
					</a>
					<a href={RELEASES_URL} target="_blank" rel="noopener noreferrer">
						{messages.footer.releases} ↗
					</a>
					<a
						href={MICROSOFT_STORE_URL}
						target="_blank"
						rel="noopener noreferrer"
					>
						Microsoft Store ↗
					</a>
					<a
						href={REDDIT_COMMUNITY_URL}
						target="_blank"
						rel="noopener noreferrer"
					>
						Reddit ↗
					</a>
					<a href={SUPPORT_URL} target="_blank" rel="noopener noreferrer">
						{messages.sections.support.heading} ↗
					</a>
				</div>
				<div className="footer-bottom">
					<a className="wordmark" href="#top">
						HTWind<span aria-hidden="true">.</span>
					</a>
					<span>© {new Date().getFullYear()} sametcn99 · GPL-3.0</span>
					<a href="#top" aria-label={messages.design.backToTop}>
						↑
					</a>
				</div>
			</div>
		</footer>
	);
}
