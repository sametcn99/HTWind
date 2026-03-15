import { Caption1 } from "@fluentui/react-components";
import { Desktop24Regular } from "@fluentui/react-icons";
import { useAppStyles } from "../../styles/appStyles";

export function Banner() {
	const styles = useAppStyles();

	return (
		<div className={styles.banner}>
			<Desktop24Regular />
			<Caption1>
				Windows widget platform with native integration and web-level
				flexibility
			</Caption1>
		</div>
	);
}
