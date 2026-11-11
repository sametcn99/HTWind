import { describe, expect, it } from "vitest";
import { interpolate } from "./interpolate";

describe("interpolate", () => {
	it("replaces named placeholders with values", () => {
		expect(
			interpolate("Released on {date} for {version}", {
				date: "Jan 02, 2026",
				version: "1.4.0",
			}),
		).toBe("Released on Jan 02, 2026 for 1.4.0");
	});

	it("stringifies number replacements", () => {
		expect(interpolate("{count} downloads", { count: 42 })).toBe(
			"42 downloads",
		);
	});

	it("keeps placeholders without a matching replacement", () => {
		expect(interpolate("Hello {name}, bye {missing}", { name: "Ada" })).toBe(
			"Hello Ada, bye {missing}",
		);
	});

	it("replaces repeated placeholders", () => {
		expect(interpolate("{v}+{v}", { v: 7 })).toBe("7+7");
	});

	it("returns the template unchanged when it has no placeholders", () => {
		expect(interpolate("no placeholders here", {})).toBe(
			"no placeholders here",
		);
	});
});
