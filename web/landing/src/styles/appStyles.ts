import { makeStyles, shorthands, tokens } from "@fluentui/react-components";

const surfaceBase = "rgba(12, 17, 24, 0.78)";
const surfaceRaised = "rgba(18, 24, 34, 0.82)";
const surfaceMuted = "rgba(255, 255, 255, 0.045)";
const borderSubtle = "rgba(255, 255, 255, 0.08)";
const borderStrong = "rgba(110, 178, 255, 0.28)";
const textPrimary = "rgba(255, 255, 255, 0.96)";
const textSecondary = "rgba(222, 231, 241, 0.74)";
const textTertiary = "rgba(203, 216, 228, 0.56)";
const accent = "#5ea8ff";
const accentStrong = "#2d7be8";
const transition = "180ms cubic-bezier(0.16, 1, 0.3, 1)";
const tablet = "@media (min-width: 768px)";
const desktop = "@media (min-width: 1024px)";
const wide = "@media (min-width: 1200px)";

export const useAppStyles = makeStyles({
	page: {
		color: textPrimary,
		minHeight: "100vh",
		backgroundColor: "transparent",
	},
	shell: {
		marginInline: "auto",
		maxWidth: "1200px",
		paddingTop: "0",
		paddingRight: "20px",
		paddingBottom: "88px",
		paddingLeft: "20px",
		display: "grid",
		rowGap: "24px",
		fontFamily: '"Segoe UI Variable Text", "Manrope", "Segoe UI", sans-serif',
		[tablet]: {
			paddingRight: "28px",
			paddingLeft: "28px",
			rowGap: "28px",
		},
		[desktop]: {
			paddingRight: "32px",
			paddingLeft: "32px",
			rowGap: "32px",
		},
	},
	banner: {
		display: "flex",
		alignItems: "center",
		justifyContent: "space-between",
		flexWrap: "wrap",
		columnGap: "16px",
		rowGap: "12px",
		width: "100%",
		maxWidth: "100%",
		...shorthands.padding("10px", "14px"),
		...shorthands.borderRadius("999px"),
		...shorthands.border("1px", "solid", borderStrong),
		backgroundColor: "rgba(23, 42, 72, 0.5)",
		color: "rgba(204, 228, 255, 0.92)",
		marginTop: "2rem",
		backdropFilter: "blur(18px)",
	},
	bannerContent: {
		display: "inline-flex",
		alignItems: "center",
		gap: "10px",
		minWidth: 0,
	},
	bannerText: {
		lineHeight: "1.5",
	},
	languageControl: {
		display: "inline-flex",
		alignItems: "center",
		gap: "10px",
		marginLeft: "auto",
	},
	languageLabel: {
		color: textTertiary,
		whiteSpace: "nowrap",
	},
	languageSelect: {
		minWidth: "140px",
		backgroundColor: "rgba(12, 17, 24, 0.72)",
		color: textPrimary,
		...shorthands.border("1px", "solid", borderSubtle),
		...shorthands.borderRadius(tokens.borderRadiusMedium),
	},
	heroCard: {
		background:
			"linear-gradient(180deg, rgba(15, 21, 31, 0.94) 0%, rgba(10, 15, 23, 0.86) 100%)",
		...shorthands.border("1px", "solid", borderSubtle),
		...shorthands.borderRadius("28px"),
		...shorthands.padding("24px"),
		display: "grid",
		rowGap: "24px",
		boxShadow: "0 24px 60px rgba(0, 0, 0, 0.28)",
		overflow: "visible",
		[tablet]: {
			...shorthands.padding("32px"),
		},
		[desktop]: {
			...shorthands.padding("40px"),
			rowGap: "28px",
		},
	},
	heroTopRow: {
		display: "grid",
		gap: "24px",
		alignItems: "start",
		[desktop]: {
			gridTemplateColumns: "minmax(0, 1.35fr) minmax(280px, 360px)",
			gap: "32px",
		},
		[wide]: {
			gap: "40px",
		},
	},
	heroCopyBlock: {
		display: "grid",
		rowGap: "20px",
	},
	heroEyebrow: {
		display: "inline-flex",
		alignItems: "center",
		width: "fit-content",
		color: "rgba(189, 220, 255, 0.94)",
		backgroundColor: "rgba(94, 168, 255, 0.12)",
		...shorthands.border("1px", "solid", "rgba(94, 168, 255, 0.26)"),
		...shorthands.borderRadius("999px"),
		...shorthands.padding("6px", "10px"),
		letterSpacing: "0.04em",
		textTransform: "uppercase",
	},
	heroHeading: {
		marginTop: "0",
		marginBottom: "0",
		fontFamily:
			'"Segoe UI Variable Display", "Space Grotesk", "Trebuchet MS", sans-serif',
		fontSize: "clamp(2.25rem, 5.6vw, 4.5rem)",
		letterSpacing: "-0.04em",
		lineHeight: "1.02",
		color: "#ffffff",
		fontWeight: "700",
		maxWidth: "12ch",
	},
	heroDescription: {
		marginTop: "0",
		marginBottom: "0",
		fontSize: "clamp(1rem, 1.85vw, 1.16rem)",
		color: textSecondary,
		maxWidth: "62ch",
		lineHeight: "1.75",
	},
	heroProofList: {
		display: "grid",
		gap: "12px",
		[tablet]: {
			gridTemplateColumns: "repeat(3, minmax(0, 1fr))",
		},
	},
	heroProofItem: {
		display: "grid",
		rowGap: "8px",
		backgroundColor: surfaceMuted,
		color: textPrimary,
		...shorthands.border("1px", "solid", borderSubtle),
		...shorthands.borderRadius(tokens.borderRadiusLarge),
		...shorthands.padding("14px"),
		lineHeight: "1.55",
	},
	releaseCard: {
		display: "grid",
		rowGap: "14px",
		width: "100%",
		background:
			"linear-gradient(180deg, rgba(255, 255, 255, 0.05) 0%, rgba(255, 255, 255, 0.025) 100%)",
		...shorthands.padding("22px"),
		...shorthands.border("1px", "solid", borderStrong),
		...shorthands.borderRadius("24px"),
		boxShadow: "inset 0 1px 0 rgba(255, 255, 255, 0.06)",
		[desktop]: {
			position: "sticky",
			top: "28px",
		},
	},
	releaseTitle: {
		color: "rgba(197, 214, 236, 0.7)",
		letterSpacing: "0.08em",
		textTransform: "uppercase",
	},
	releaseValue: {
		color: "#ffffff",
		fontWeight: tokens.fontWeightSemibold,
		fontSize: "1.45rem",
		lineHeight: "1.2",
	},
	releaseMeta: {
		color: textTertiary,
		lineHeight: "1.6",
	},
	releaseChannelList: {
		display: "grid",
		gap: "10px",
	},
	releaseChannelItem: {
		display: "grid",
		rowGap: "4px",
		backgroundColor: "rgba(255, 255, 255, 0.04)",
		...shorthands.border("1px", "solid", borderSubtle),
		...shorthands.borderRadius(tokens.borderRadiusMedium),
		...shorthands.padding("12px"),
	},
	releaseChannelLabel: {
		color: textPrimary,
		fontWeight: tokens.fontWeightSemibold,
	},
	buttonRow: {
		display: "flex",
		flexWrap: "wrap",
		gap: "12px",
		width: "100%",
		"& > *": {
			flexGrow: 1,
			flexBasis: "calc(50% - 12px)",
			minWidth: "min(100%, 220px)",
		},
		[tablet]: {
			"& > :first-child": {
				flexBasis: "100%",
			},
		},
		"@media (max-width: 620px)": {
			"& > *": {
				flexBasis: "100%",
			},
		},
	},
	primaryButton: {
		width: "100%",
		minHeight: "48px",
		backgroundColor: accentStrong,
		color: "#ffffff",
		...shorthands.border("1px", "solid", accentStrong),
		fontWeight: tokens.fontWeightSemibold,
		boxShadow: "0 10px 24px rgba(45, 123, 232, 0.25)",
		":hover": {
			backgroundColor: accent,
			border: `1px solid ${accent}`,
			color: "#ffffff",
			transform: "translateY(-1px)",
		},
		":active": {
			backgroundColor: "#2268c9",
			border: "1px solid #2268c9",
			color: "#ffffff",
		},
	},
	ghostButton: {
		width: "100%",
		minHeight: "48px",
		backgroundColor: surfaceMuted,
		...shorthands.border("1px", "solid", borderSubtle),
		color: textPrimary,
		fontWeight: tokens.fontWeightSemibold,
		":hover": {
			backgroundColor: "rgba(255, 255, 255, 0.08)",
			border: "1px solid rgba(255, 255, 255, 0.14)",
			color: textPrimary,
			transform: "translateY(-1px)",
		},
		":active": {
			backgroundColor: "rgba(255, 255, 255, 0.06)",
		},
	},
	dropdownContainer: {
		position: "relative",
		width: "100%",
	},
	dropdownButton: {
		width: "100%",
		minHeight: "48px",
		backgroundColor: accentStrong,
		color: "#ffffff",
		...shorthands.border("1px", "solid", accentStrong),
		fontWeight: tokens.fontWeightSemibold,
		boxShadow: "0 10px 24px rgba(45, 123, 232, 0.25)",
		":hover": {
			backgroundColor: accent,
			border: `1px solid ${accent}`,
			color: "#ffffff",
			transform: "translateY(-1px)",
		},
		":active": {
			backgroundColor: "#2268c9",
			border: "1px solid #2268c9",
			color: "#ffffff",
		},
		":disabled": {
			backgroundColor: "rgba(255, 255, 255, 0.08)",
			border: "1px solid rgba(255, 255, 255, 0.08)",
			boxShadow: "none",
			color: textTertiary,
		},
	},
	dropdownMenu: {
		position: "absolute",
		top: "calc(100% + 10px)",
		left: "0",
		width: "min(100%, 460px)",
		maxHeight: "360px",
		overflowY: "auto",
		zIndex: "30",
		backgroundColor: "rgba(9, 13, 19, 0.96)",
		backdropFilter: "blur(22px)",
		...shorthands.border("1px", "solid", "rgba(94, 168, 255, 0.2)"),
		...shorthands.borderRadius("20px"),
		...shorthands.padding("10px"),
		boxShadow: "0 24px 48px rgba(0, 0, 0, 0.42)",
	},
	dropdownTitle: {
		color: textPrimary,
		fontWeight: tokens.fontWeightSemibold,
		...shorthands.padding("8px", "10px", "2px", "10px"),
		letterSpacing: "0.01em",
	},
	dropdownHint: {
		color: textTertiary,
		display: "block",
		...shorthands.padding("0", "10px", "8px", "10px"),
		lineHeight: "1.5",
	},
	dropdownItem: {
		display: "grid",
		rowGap: "4px",
		...shorthands.padding("12px"),
		...shorthands.borderRadius(tokens.borderRadiusMedium),
		textDecorationLine: "none",
		color: textPrimary,
		...shorthands.border("1px", "solid", "transparent"),
		":hover": {
			backgroundColor: "rgba(255, 255, 255, 0.06)",
			border: `1px solid ${borderSubtle}`,
		},
	},
	dropdownItemRecommended: {
		backgroundColor: "rgba(94, 168, 255, 0.08)",
		...shorthands.border("1px", "solid", "rgba(94, 168, 255, 0.18)"),
	},
	dropdownItemTitle: {
		fontWeight: tokens.fontWeightSemibold,
	},
	dropdownItemDescription: {
		color: textSecondary,
		lineHeight: "1.5",
	},
	dropdownItemMeta: {
		color: textTertiary,
	},
	dropdownList: {
		listStyleType: "none",
		...shorthands.padding(0),
		...shorthands.margin(0),
		display: "grid",
		gap: "6px",
	},
	statsGrid: {
		display: "grid",
		gap: "12px",
		[tablet]: {
			gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
		},
		[desktop]: {
			gridTemplateColumns: "repeat(4, minmax(0, 1fr))",
		},
	},
	statCard: {
		backgroundColor: surfaceRaised,
		...shorthands.border("1px", "solid", borderSubtle),
		...shorthands.borderRadius("22px"),
		...shorthands.padding("18px"),
		display: "grid",
		rowGap: "8px",
		minHeight: "112px",
		transition: `transform ${transition}, border-color ${transition}`,
		":hover": {
			transform: "translateY(-2px)",
			border: "1px solid rgba(255, 255, 255, 0.12)",
		},
	},
	statCardSpanTwo: {
		[desktop]: {
			gridColumn: "span 2",
		},
	},
	statCardSpanFour: {
		[desktop]: {
			gridColumn: "1 / -1",
		},
	},
	statValue: {
		color: "#ffffff",
		fontFamily:
			'"Segoe UI Variable Display", "Space Grotesk", "Trebuchet MS", sans-serif',
		fontSize: "1.45rem",
		fontWeight: tokens.fontWeightSemibold,
		lineHeight: "1.2",
	},
	statLabel: {
		color: textTertiary,
		lineHeight: "1.55",
	},
	contentGrid: {
		display: "grid",
		gap: "16px",
		[tablet]: {
			gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
		},
		[desktop]: {
			gridTemplateColumns: "repeat(3, minmax(0, 1fr))",
		},
	},
	twoColumnContentGrid: {
		display: "grid",
		gap: "16px",
		[desktop]: {
			gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
		},
	},
	card: {
		backgroundColor: surfaceRaised,
		...shorthands.border("1px", "solid", borderSubtle),
		...shorthands.borderRadius("22px"),
		...shorthands.padding("22px"),
		display: "grid",
		rowGap: "12px",
		transition: `transform ${transition}, border-color ${transition}, background-color ${transition}`,
		":hover": {
			backgroundColor: "rgba(22, 29, 41, 0.86)",
			border: "1px solid rgba(255, 255, 255, 0.12)",
			transform: "translateY(-2px)",
		},
	},
	cardIcon: {
		width: "44px",
		height: "44px",
		display: "inline-flex",
		alignItems: "center",
		justifyContent: "center",
		color: accent,
		backgroundColor: "rgba(94, 168, 255, 0.08)",
		...shorthands.borderRadius("14px"),
		...shorthands.border("1px", "solid", "rgba(94, 168, 255, 0.18)"),
	},
	featureTitle: {
		color: textPrimary,
		lineHeight: "1.35",
	},
	featureDescription: {
		color: textSecondary,
		lineHeight: "1.72",
		marginTop: "0",
		marginBottom: "0",
	},
	screenshotImage: {
		width: "100%",
		height: "100%",
		minHeight: "220px",
		display: "block",
		objectFit: "cover",
		transition: "transform 0.6s cubic-bezier(0.16, 1, 0.3, 1)",
		":hover": {
			transform: "scale(1.02)",
		},
	},
	screenshotsGrid: {
		display: "grid",
		gap: "16px",
		gridTemplateColumns: "repeat(1, minmax(0, 1fr))",
		[tablet]: {
			gridTemplateColumns: "repeat(3, minmax(0, 1fr))",
		},
	},
	screenshotCard: {
		backgroundColor: "rgba(0, 0, 0, 0.28)",
		...shorthands.border("1px", "solid", borderSubtle),
		...shorthands.borderRadius("22px"),
		overflow: "hidden",
		position: "relative",
	},
	widgetsCard: {
		backgroundColor: surfaceBase,
		...shorthands.border("1px", "solid", borderSubtle),
		...shorthands.borderRadius("24px"),
		...shorthands.padding("24px"),
		display: "grid",
		rowGap: "16px",
		[tablet]: {
			...shorthands.padding("30px"),
		},
	},
	longFormSection: {
		backgroundColor: surfaceBase,
		...shorthands.border("1px", "solid", borderSubtle),
		...shorthands.borderRadius("24px"),
		...shorthands.padding("24px"),
		display: "grid",
		rowGap: "16px",
		[tablet]: {
			...shorthands.padding("30px"),
		},
	},
	sectionHeading: {
		marginTop: "0",
		marginBottom: "0",
		color: "#ffffff",
		fontFamily:
			'"Segoe UI Variable Display", "Space Grotesk", "Trebuchet MS", sans-serif',
		fontSize: "clamp(1.55rem, 3vw, 2.4rem)",
		lineHeight: "1.12",
		letterSpacing: "-0.03em",
	},
	sectionLead: {
		marginTop: "0",
		marginBottom: "0",
		color: textSecondary,
		lineHeight: "1.76",
		maxWidth: "74ch",
	},
	bulletList: {
		marginTop: "0",
		marginBottom: "0",
		paddingLeft: "20px",
		display: "grid",
		rowGap: "12px",
		color: textSecondary,
		lineHeight: "1.72",
	},
	communityNotice: {
		marginTop: "0",
		marginBottom: "0",
		color: "rgba(225, 236, 248, 0.82)",
		lineHeight: "1.7",
	},
	communitySection: {
		background:
			"linear-gradient(180deg, rgba(14, 21, 31, 0.9) 0%, rgba(10, 16, 24, 0.82) 100%)",
		...shorthands.border("1px", "solid", borderStrong),
		...shorthands.borderRadius("24px"),
		...shorthands.padding("24px"),
		display: "grid",
		rowGap: "16px",
		[tablet]: {
			...shorthands.padding("30px"),
		},
	},
	faqGrid: {
		display: "grid",
		gap: "12px",
		[desktop]: {
			gridTemplateColumns: "repeat(2, minmax(0, 1fr))",
		},
	},
	faqCard: {
		backgroundColor: surfaceRaised,
		...shorthands.border("1px", "solid", borderSubtle),
		...shorthands.borderRadius("22px"),
		...shorthands.padding("18px"),
		display: "grid",
		rowGap: "10px",
	},
	faqCardWide: {
		[desktop]: {
			gridColumn: "1 / -1",
		},
	},
	faqQuestion: {
		marginTop: "0",
		marginBottom: "0",
		color: textPrimary,
		fontWeight: tokens.fontWeightSemibold,
		lineHeight: "1.4",
	},
	faqAnswer: {
		marginTop: "0",
		marginBottom: "0",
		color: textSecondary,
		lineHeight: "1.72",
	},
	widgetList: {
		display: "flex",
		flexWrap: "wrap",
		gap: "10px",
	},
	widgetBadge: {
		...shorthands.borderRadius("999px"),
		...shorthands.padding("2px", "4px"),
	},
	screenshotMeta: {
		marginTop: "-4px",
		marginBottom: "0",
		color: textTertiary,
		lineHeight: "1.6",
	},
	footer: {
		color: "rgba(255, 255, 255, 0.42)",
		textAlign: "center",
		display: "grid",
		gap: "8px",
		...shorthands.padding("8px", "0", "0", "0"),
	},
	footerLinks: {
		display: "flex",
		justifyContent: "center",
		flexWrap: "wrap",
		gap: "14px",
	},
	footerLink: {
		color: textTertiary,
		textDecorationLine: "none",
		":hover": {
			color: textPrimary,
		},
	},
	lightboxSurface: {
		padding: 0,
		backgroundColor: "rgba(0, 0, 0, 0.96)",
		maxWidth: "100vw",
		maxHeight: "100vh",
		width: "100vw",
		height: "100vh",
		...shorthands.border("0"),
		boxShadow: "none",
	},
	lightboxContent: {
		height: "100%",
		width: "100%",
		display: "flex",
		alignItems: "center",
		justifyContent: "center",
		overflow: "hidden",
		position: "relative",
		...shorthands.padding(0),
	},
	lightboxBody: {
		width: "100%",
		height: "100%",
		...shorthands.padding(0),
	},
	lightboxImage: {
		maxWidth: "100%",
		maxHeight: "100%",
		objectFit: "contain",
	},
	closeButton: {
		position: "absolute",
		top: "20px",
		right: "20px",
		zIndex: "1000",
		backgroundColor: "rgba(255, 255, 255, 0.08)",
		color: "#ffffff",
		backdropFilter: "blur(14px)",
		":hover": {
			backgroundColor: "rgba(255, 255, 255, 0.14)",
			color: "#ffffff",
		},
	},
	clickableScreenshot: {
		cursor: "zoom-in",
	},
	screenshotButton: {
		backgroundColor: "transparent",
		...shorthands.border("0"),
		paddingTop: "0",
		paddingRight: "0",
		paddingBottom: "0",
		paddingLeft: "0",
		width: "100%",
		display: "block",
		textAlign: "left",
		transition: `transform ${transition}, box-shadow ${transition}`,
		":hover": {
			transform: "translateY(-2px)",
			boxShadow: "0 16px 32px rgba(0, 0, 0, 0.24)",
		},
	},
	storeBadgeContainer: {
		marginTop: "2px",
	},
	storeBadgeImage: {
		display: "block",
		borderRadius: "12px",
	},
	lightboxTransformWrapper: {
		width: "100%",
		height: "100%",
		display: "flex",
		justifyContent: "center",
		alignItems: "center",
	},
});
