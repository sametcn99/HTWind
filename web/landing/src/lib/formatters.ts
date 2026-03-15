export function formatReleaseDate(
	isoDate: string,
	locale: string,
	unknownLabel: string,
): string {
	const date = new Date(isoDate);
	if (Number.isNaN(date.getTime())) {
		return unknownLabel;
	}

	return new Intl.DateTimeFormat(locale, {
		year: "numeric",
		month: "short",
		day: "2-digit",
	}).format(date);
}

export function formatFileSize(
	bytes: number,
	locale: string,
	unknownLabel: string,
): string {
	if (!Number.isFinite(bytes) || bytes < 0) {
		return unknownLabel;
	}

	if (bytes < 1024) {
		return `${new Intl.NumberFormat(locale).format(bytes)} B`;
	}

	const units = ["KB", "MB", "GB"];
	let value = bytes / 1024;
	let unitIndex = 0;

	while (value >= 1024 && unitIndex < units.length - 1) {
		value /= 1024;
		unitIndex += 1;
	}

	const formattedValue = new Intl.NumberFormat(locale, {
		maximumFractionDigits: value >= 100 ? 0 : 1,
		minimumFractionDigits: value >= 100 ? 0 : 1,
	}).format(value);

	return `${formattedValue} ${units[unitIndex]}`;
}
