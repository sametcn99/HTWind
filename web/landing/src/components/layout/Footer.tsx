import { Caption1 } from "@fluentui/react-components";
import { useAppStyles } from "../../styles/appStyles";

export function Footer() {
	const styles = useAppStyles();

	return (
		<footer className={styles.footer}>
			<Caption1>
				HTWind | Open-source desktop widget manager for Windows 11
			</Caption1>
		</footer>
	);
}
