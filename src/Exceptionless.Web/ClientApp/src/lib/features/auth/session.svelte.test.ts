import { beforeEach, describe, expect, it, vi } from 'vitest';

const shutdownIntercomSession = vi.hoisted(() => vi.fn());
const accessToken = vi.hoisted(() => ({ current: 'token_123' as null | string }));

vi.mock('#features/intercom/session.ts', () => ({ shutdownIntercomSession }));
vi.mock('./state.svelte', () => ({ accessToken }));

import { clearAuthenticationSession } from './session.svelte';

describe('clearAuthenticationSession', () => {
    beforeEach(() => {
        localStorage.clear();
        accessToken.current = 'token_123';
    });

    it('removes saved Exie chats, including when an expired session ends it', () => {
        localStorage.setItem('exie:conversations:v1:user-1:organization-1', '[]');
        localStorage.setItem('organization', 'organization-1');

        clearAuthenticationSession('');

        expect(localStorage.getItem('exie:conversations:v1:user-1:organization-1')).toBeNull();
        expect(localStorage.getItem('organization')).toBe('organization-1');
        expect(accessToken.current).toBe('');
        expect(shutdownIntercomSession).toHaveBeenCalledOnce();
    });
});
