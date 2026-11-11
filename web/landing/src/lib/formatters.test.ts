import { describe, expect, it } from "vitest";
import { formatFileSize, formatReleaseDate } from "./formatters";

describe("formatReleaseDate", () => {
	it("formats a valid ISO date in the requested locale", () => {
		const formatted = formatReleaseDate("2026-01-02T12:00:00Z", "en-US", "?");
		expect(formatted).toBe(
			new Intl.DateTimeFormat("en-US", {
				year: "numeric",
				month: "short",
				day: "2-digit",
			}).format(new Date("2026-01-02T12:00:00Z")),
		);
	});

	it("returns the unknown label for invalid dates", () => {
		expect(formatReleaseDate("not-a-date", "en-US", "unknown")).toBe("unknown");
	});

	it("respects locale-specific formatting", () => {
		const formatted = formatReleaseDate("2026-03-05T00:00:00Z", "de-DE", "?");
		expect(formatted).toContain("05");
		expect(formatted).toContain("2026");
	});
});

describe("formatFileSize", () => {
	it("formats bytes below 1 KB", () => {
		expect(formatFileSize(512, "en-US", "?")).toBe("512 B");
		expect(formatFileSize(0, "en-US", "?")).toBe("0 B");
	});

	it("formats kilobytes with one fraction digit", () => {
		expect(formatFileSize(1536, "en-US", "?")).toBe("1.5 KB");
		expect(formatFileSize(1024, "en-US", "?")).toBe("1.0 KB");
	});

	it("formats megabytes", () => {
		expect(formatFileSize(5 * 1024 * 1024, "en-US", "?")).toBe("5.0 MB");
	});

	it("caps the unit table at gigabytes", () => {
		expect(formatFileSize(3 * 1024 * 1024 * 1024, "en-US", "?")).toBe("3.0 GB");
		expect(
			formatFileSize(5 * 1024 * 1024 * 1024 * 1024, "en-US", "?"),
		).toContain("GB");
	});

	it("drops fraction digits once the value reaches 100", () => {
		expect(formatFileSize(150 * 1024 * 1024, "en-US", "?")).toBe("150 MB");
	});

	it("uses locale-specific decimal separators", () => {
		expect(formatFileSize(1536, "de-DE", "?")).toBe("1,5 KB");
	});

	it("returns the unknown label for negative or non-finite sizes", () => {
		expect(formatFileSize(-1, "en-US", "unknown")).toBe("unknown");
		expect(formatFileSize(Number.NaN, "en-US", "unknown")).toBe("unknown");
		expect(formatFileSize(Number.POSITIVE_INFINITY, "en-US", "unknown")).toBe(
			"unknown",
		);
	});
});
