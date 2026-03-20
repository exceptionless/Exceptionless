import { supportIssuesHref } from '$features/shared/help-links';
import { describe, expect, it, vi } from 'vitest';

import { openSupportChat } from './chat';

describe('openSupportChat', () => {
    it('opens Intercom messages when the messenger is available', () => {
        const intercom = {
            showMessages: vi.fn()
        };
        const openWindow = vi.fn();

        openSupportChat(intercom, openWindow);

        expect(intercom.showMessages).toHaveBeenCalledOnce();
        expect(openWindow).not.toHaveBeenCalled();
    });

    it('falls back to the support issues page when the messenger is unavailable', () => {
        const openWindow = vi.fn();

        openSupportChat(undefined, openWindow);

        expect(openWindow).toHaveBeenCalledWith(supportIssuesHref, '_blank', 'noopener,noreferrer');
    });
});
