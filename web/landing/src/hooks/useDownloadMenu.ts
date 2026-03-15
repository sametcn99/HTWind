import { useEffect, useRef, useState } from "react";

export function useDownloadMenu() {
	const [isOpen, setIsOpen] = useState(false);
	const containerRef = useRef<HTMLDivElement | null>(null);

	useEffect(() => {
		if (!isOpen) {
			return;
		}

		function handleDocumentPointerDown(event: PointerEvent): void {
			const target = event.target;
			if (!(target instanceof Node)) {
				return;
			}

			if (!containerRef.current?.contains(target)) {
				setIsOpen(false);
			}
		}

		function handleEscape(event: KeyboardEvent): void {
			if (event.key === "Escape") {
				setIsOpen(false);
			}
		}

		window.addEventListener("pointerdown", handleDocumentPointerDown);
		window.addEventListener("keydown", handleEscape);

		return () => {
			window.removeEventListener("pointerdown", handleDocumentPointerDown);
			window.removeEventListener("keydown", handleEscape);
		};
	}, [isOpen]);

	return {
		containerRef,
		isOpen,
		toggle: () => setIsOpen((value) => !value),
		close: () => setIsOpen(false),
	};
}
