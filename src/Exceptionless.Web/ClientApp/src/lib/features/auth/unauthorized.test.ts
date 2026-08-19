import { beforeEach, describe, expect, it, vi } from 'vitest';

const clearAuthenticationSession = vi.hoisted(() => vi.fn());
const accessToken = vi.hoisted(() => ({ current: 'token_123' as null | string }));

vi.mock('./session.svelte', () => ({ clearAuthenticationSession }));
vi.mock('./state.svelte', () => ({ accessToken }));

import { handleUnexpectedUnauthorized } from './unauthorized';

describe('handleUnexpectedUnauthorized', () => {
    beforeEach(() => {
        accessToken.current = 'token_123';
        clearAuthenticationSession.mockReset();
    });

    it('clears the authenticated session after an unexpected unauthorized response', () => {
        expect(handleUnexpectedUnauthorized(401)).toBe(true);
        expect(clearAuthenticationSession).toHaveBeenCalledExactlyOnceWith('');
    });

    it('ignores expected unauthorized responses and duplicate session cleanup', () => {
        expect(handleUnexpectedUnauthorized(401, [401])).toBe(false);

        accessToken.current = null;
        expect(handleUnexpectedUnauthorized(401)).toBe(false);
        expect(clearAuthenticationSession).not.toHaveBeenCalled();
    });
});
