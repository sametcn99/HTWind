import {
	Button,
	Dialog,
	DialogBody,
	DialogContent,
	DialogSurface,
} from "@fluentui/react-components";
import { Dismiss24Regular } from "@fluentui/react-icons";
import { useRef } from "react";
import {
	type ReactZoomPanPinchRef,
	TransformComponent,
	TransformWrapper,
} from "react-zoom-pan-pinch";
import type { ScreenshotItem } from "../../config/types";
import { useAppStyles } from "../../styles/appStyles";

type ScreenshotLightboxProps = {
	selectedImage: ScreenshotItem | null;
	onClose: () => void;
};

export function ScreenshotLightbox({
	selectedImage,
	onClose,
}: ScreenshotLightboxProps) {
	const styles = useAppStyles();
	const lightboxTransformRef = useRef<ReactZoomPanPinchRef | null>(null);

	return (
		<Dialog
			open={!!selectedImage}
			onOpenChange={(_, data) => !data.open && onClose()}
		>
			<DialogSurface className={styles.lightboxSurface}>
				<DialogBody className={styles.lightboxBody}>
					<DialogContent className={styles.lightboxContent}>
						<Button
							appearance="subtle"
							icon={<Dismiss24Regular />}
							className={styles.closeButton}
							onClick={onClose}
							aria-label="Close"
						/>
						{selectedImage && (
							<TransformWrapper
								ref={lightboxTransformRef}
								initialScale={1}
								minScale={1}
								maxScale={8}
								centerOnInit={true}
								centerZoomedOut={true}
							>
								<TransformComponent
									wrapperClass={styles.lightboxTransformWrapper}
									contentClass={styles.lightboxTransformWrapper}
								>
									<img
										src={selectedImage.src}
										alt={selectedImage.alt}
										className={styles.lightboxImage}
										onLoad={() => {
											lightboxTransformRef.current?.resetTransform(0);
											lightboxTransformRef.current?.centerView(1, 0);
										}}
									/>
								</TransformComponent>
							</TransformWrapper>
						)}
					</DialogContent>
				</DialogBody>
			</DialogSurface>
		</Dialog>
	);
}
