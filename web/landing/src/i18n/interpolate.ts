export function interpolate(
	template: string,
	replacements: Record<string, string | number>,
): string {
	return template.replace(/\{(\w+)\}/g, (match, key: string) => {
		const value = replacements[key];
		return value === undefined ? match : String(value);
	});
}
