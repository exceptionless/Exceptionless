export function preserveDetailSheetForAssistant(event: PointerEvent): void {
    if (event.target instanceof Element && event.target.closest('[data-assistant-trigger]')) {
        event.preventDefault();
    }
}
