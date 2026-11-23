import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { useDownloadMenu } from "./useDownloadMenu";

function Menu() {
	const { containerRef, isOpen, toggle, close } = useDownloadMenu();
	return (
		<>
			<div ref={containerRef}>
				<button type="button" onClick={toggle} aria-expanded={isOpen}>
					Toggle
				</button>
				{isOpen && (
					<button type="button" onClick={close}>
						Download
					</button>
				)}
			</div>
			<button type="button">Outside</button>
		</>
	);
}

afterEach(cleanup);

describe("useDownloadMenu", () => {
	it("toggles repeatedly without retaining a stale open state", () => {
		render(<Menu />);
		const toggle = screen.getByRole("button", { name: "Toggle" });
		for (let index = 0; index < 3; index++) {
			fireEvent.click(toggle);
			expect(toggle.getAttribute("aria-expanded")).toBe("true");
			fireEvent.click(toggle);
			expect(toggle.getAttribute("aria-expanded")).toBe("false");
		}
	});

	it("keeps inside pointer events open and closes on an outside pointer", () => {
		render(<Menu />);
		fireEvent.click(screen.getByRole("button", { name: "Toggle" }));
		fireEvent.pointerDown(screen.getByRole("button", { name: "Download" }));
		expect(screen.queryByRole("button", { name: "Download" })).not.toBeNull();
		fireEvent.pointerDown(screen.getByRole("button", { name: "Outside" }));
		expect(screen.queryByRole("button", { name: "Download" })).toBeNull();
	});

	it("closes with Escape and can reopen after dismissal", () => {
		render(<Menu />);
		const toggle = screen.getByRole("button", { name: "Toggle" });
		fireEvent.click(toggle);
		fireEvent.keyDown(window, { key: "Enter" });
		expect(toggle.getAttribute("aria-expanded")).toBe("true");
		fireEvent.keyDown(window, { key: "Escape" });
		expect(toggle.getAttribute("aria-expanded")).toBe("false");
		fireEvent.click(toggle);
		fireEvent.click(screen.getByRole("button", { name: "Download" }));
		expect(toggle.getAttribute("aria-expanded")).toBe("false");
	});

	it("ignores pointer events with a non-Node target", () => {
		render(<Menu />);
		fireEvent.click(screen.getByRole("button", { name: "Toggle" }));
		fireEvent.pointerDown(window);
		expect(screen.queryByRole("button", { name: "Download" })).not.toBeNull();
	});
});
