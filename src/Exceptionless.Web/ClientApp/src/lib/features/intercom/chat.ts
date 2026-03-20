import { supportIssuesHref } from '$features/shared/help-links';

export interface IntercomMessenger {
    showMessages: () => void;
}

export function openSupportChat(intercom: IntercomMessenger | null | undefined, openWindow: typeof globalThis.open = globalThis.open) {
    if (intercom) {
        intercom.showMessages();
        return;
    }

    openWindow?.(supportIssuesHref, '_blank', 'noopener,noreferrer');
}
