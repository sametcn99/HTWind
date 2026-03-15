import { Link } from "@fluentui/react-components";
import {
	GITHUB_DISCUSSIONS_URL,
	GITHUB_REPOSITORY_URL,
	RELEASES_URL,
} from "../../config/constants";
import { useLocale } from "../../i18n/LocaleContext";
import { useAppStyles } from "../../styles/appStyles";

export function Footer() {
	const styles = useAppStyles();
	const { messages } = useLocale();

	return (
		<footer className={styles.footer}>
			<div className={styles.footerLinks}>
				<Link
					href={GITHUB_REPOSITORY_URL}
					target="_blank"
					rel="noreferrer"
					className={styles.footerLink}
				>
					{messages.footer.github}
				</Link>
				<Link
					href={RELEASES_URL}
					target="_blank"
					rel="noreferrer"
					className={styles.footerLink}
				>
					{messages.footer.releases}
				</Link>
				<Link
					href={GITHUB_DISCUSSIONS_URL}
					target="_blank"
					rel="noreferrer"
					className={styles.footerLink}
				>
					{messages.footer.discussions}
				</Link>
			</div>
		</footer>
	);
}
