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

		window.addEventListener("pointerdown", handleDocumentPointerDown);

		return () => {
			window.removeEventListener("pointerdown", handleDocumentPointerDown);
		};
	}, [isOpen]);

	return {
		containerRef,
		isOpen,
		toggle: () => setIsOpen((value) => !value),
		close: () => setIsOpen(false),
	};
}
