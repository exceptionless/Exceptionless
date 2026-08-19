export const INTERACTIVE_OVERLAY_OPENED_EVENT = 'exceptionless:interactive-overlay-opened';

const openOverlays = new Set<symbol>();

export function hasOpenInteractiveOverlay(): boolean {
    return openOverlays.size > 0;
}

export function updateOpenInteractiveOverlay(overlayId: symbol, open: boolean): void {
    if (open) {
        openOverlays.add(overlayId);
    } else {
        openOverlays.delete(overlayId);
    }
}
