import { useEffect, useRef } from "react";
import { TransformComponent, TransformWrapper } from "react-zoom-pan-pinch";
import type { ScreenshotItem } from "../../config/types";
import { useLocale } from "../../i18n/LocaleContext";

type ScreenshotLightboxProps = {
	selectedImage: ScreenshotItem | null;
	onClose: () => void;
};

export function ScreenshotLightbox({
	selectedImage,
	onClose,
}: ScreenshotLightboxProps) {
	const { messages } = useLocale();
	const dialogRef = useRef<HTMLDialogElement>(null);
	useEffect(() => {
		const dialog = dialogRef.current;
		if (!selectedImage || !dialog) return;
		const previousOverflow = document.body.style.overflow;
		dialog.showModal();
		document.body.style.overflow = "hidden";
		return () => {
			dialog.close();
			document.body.style.overflow = previousOverflow;
		};
	}, [selectedImage]);
	return (
		<dialog
			ref={dialogRef}
			className="screenshot-dialog"
			aria-label={selectedImage?.alt}
			onCancel={(event) => {
				event.preventDefault();
				onClose();
			}}
		>
			{selectedImage && (
				<TransformWrapper
					key={selectedImage.src}
					minScale={1}
					maxScale={5}
					centerOnInit
				>
					{({ zoomIn, zoomOut, resetTransform }) => (
						<>
							<div className="lightbox-toolbar">
								<span>{selectedImage.alt}</span>
								<div>
									<button
										type="button"
										aria-label={messages.design.zoomOut}
										onClick={() => zoomOut()}
									>
										−
									</button>
									<button
										type="button"
										aria-label={messages.design.resetZoom}
										onClick={() => resetTransform()}
									>
										1:1
									</button>
									<button
										type="button"
										aria-label={messages.design.zoomIn}
										onClick={() => zoomIn()}
									>
										+
									</button>
									<button
										type="button"
										aria-label={messages.lightbox.close}
										onClick={onClose}
									>
										×
									</button>
								</div>
							</div>
							<TransformComponent
								wrapperClass="lightbox-viewport"
								contentClass="lightbox-image"
							>
								<img src={selectedImage.src} alt={selectedImage.alt} />
							</TransformComponent>
						</>
					)}
				</TransformWrapper>
			)}
		</dialog>
	);
}
