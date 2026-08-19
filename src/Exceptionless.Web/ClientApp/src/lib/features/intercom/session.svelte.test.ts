import { beforeEach, describe, expect, it, vi } from 'vitest';

const hide = vi.hoisted(() => vi.fn());
const shutdown = vi.hoisted(() => vi.fn());

vi.mock('@intercom/messenger-js-sdk', () => ({ hide, shutdown }));

import { shutdownIntercomSession } from './session';

describe('shutdownIntercomSession', () => {
    beforeEach(() => {
        hide.mockReset();
        shutdown.mockReset();
        window.Intercom = vi.fn();
        document.cookie = 'intercom-id-app=visitor; Path=/';
        document.cookie = 'intercom-session-app=session; Path=/';
        document.cookie = 'unrelated-cookie=keep; Path=/';
    });

    it('shuts down the SDK and clears all Intercom cookies', () => {
        shutdownIntercomSession();

        expect(hide).toHaveBeenCalledOnce();
        expect(shutdown).toHaveBeenCalledOnce();
        expect(document.cookie).not.toContain('intercom-id-app');
        expect(document.cookie).not.toContain('intercom-session-app');
        expect(document.cookie).toContain('unrelated-cookie=keep');
    });

    it('still clears cookies when tracking prevention blocks the SDK', () => {
        window.Intercom = undefined;

        expect(() => shutdownIntercomSession()).not.toThrow();
        expect(hide).not.toHaveBeenCalled();
        expect(shutdown).not.toHaveBeenCalled();
        expect(document.cookie).not.toContain('intercom-id-app');
        expect(document.cookie).not.toContain('intercom-session-app');
    });
});
