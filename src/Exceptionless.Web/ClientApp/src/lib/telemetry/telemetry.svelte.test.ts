import { render } from '@testing-library/svelte';
import { tick } from 'svelte';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import Telemetry from './Telemetry.svelte';

const session = vi.hoisted(() => ({
    endSession: vi.fn().mockResolvedValue(undefined),
    setUserIdentity: vi.fn().mockResolvedValue(undefined)
}));

vi.mock('$app/navigation', () => ({ afterNavigate: vi.fn() }));
vi.mock('$app/state', () => ({ page: {} }));
vi.mock('$features/auth/exceptionless-session', () => session);
vi.mock('@exceptionless/browser', () => ({ Exceptionless: {} }));

describe('Telemetry authentication', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('waits for the authenticated profile without ending the session', async () => {
        const view = render(Telemetry, { authenticated: true });
        await tick();

        expect(session.endSession).not.toHaveBeenCalled();
        expect(session.setUserIdentity).not.toHaveBeenCalled();

        await view.rerender({ authenticated: true, userId: 'user@example.test', userName: 'Session User' });

        expect(session.endSession).not.toHaveBeenCalled();
        expect(session.setUserIdentity).toHaveBeenCalledExactlyOnceWith('user@example.test', 'Session User');
    });

    it('preserves the session while authenticated profile data is temporarily unavailable', async () => {
        const view = render(Telemetry, { authenticated: true, userId: 'user@example.test' });
        await tick();

        await view.rerender({ authenticated: true, userId: undefined });

        expect(session.endSession).not.toHaveBeenCalled();
        expect(session.setUserIdentity).toHaveBeenCalledExactlyOnceWith('user@example.test', undefined);
    });

    it('ends the session when authentication is lost even if the profile is still cached', async () => {
        const view = render(Telemetry, { authenticated: true, userId: 'user@example.test' });
        await tick();

        await view.rerender({ authenticated: false, userId: 'user@example.test' });

        expect(session.endSession).toHaveBeenCalledOnce();
        expect(session.setUserIdentity).toHaveBeenCalledOnce();
    });
});
