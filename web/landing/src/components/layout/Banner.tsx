import { Caption1, Select } from "@fluentui/react-components";
import { Desktop24Regular } from "@fluentui/react-icons";
import { useLocale } from "../../i18n/LocaleContext";
import { type LocaleCode, SUPPORTED_LOCALES } from "../../i18n/locales";
import { useAppStyles } from "../../styles/appStyles";

export function Banner() {
	const styles = useAppStyles();
	const { locale, messages, changeLocale } = useLocale();

	return (
		<div className={styles.banner}>
			<div className={styles.bannerContent}>
				<Desktop24Regular />
				<Caption1 className={styles.bannerText}>
					{messages.banner.text}
				</Caption1>
			</div>
			<div className={styles.languageControl}>
				<Caption1 className={styles.languageLabel}>
					{messages.languageSelector.label}
				</Caption1>
				<Select
					value={locale}
					size="small"
					className={styles.languageSelect}
					aria-label={messages.languageSelector.ariaLabel}
					onChange={(event) => changeLocale(event.target.value as LocaleCode)}
				>
					{SUPPORTED_LOCALES.map((supportedLocale) => (
						<option key={supportedLocale} value={supportedLocale}>
							{messages.languageSelector.options[supportedLocale]}
						</option>
					))}
				</Select>
			</div>
		</div>
	);
}
