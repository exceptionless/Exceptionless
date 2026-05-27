import { describe, expect, it } from 'vitest';

import { resolveDisplayMessage } from '../resolve-message';

describe('resolveDisplayMessage', () => {
    describe('when realtime message has not been received (undefined)', () => {
        it('returns persisted message over fallback', () => {
            expect(resolveDisplayMessage(undefined, 'Active outage', 'Scheduled maintenance')).toBe('Active outage');
        });

        it('returns fallback when no persisted message', () => {
            expect(resolveDisplayMessage(undefined, null, 'Scheduled maintenance')).toBe('Scheduled maintenance');
        });

        it('returns null when no persisted or fallback message', () => {
            expect(resolveDisplayMessage(undefined, null, null)).toBeNull();
        });
    });

    describe('when realtime message is received (string)', () => {
        it('returns the realtime message', () => {
            expect(resolveDisplayMessage('Realtime alert', 'Old persisted', 'Fallback')).toBe('Realtime alert');
        });

        it('ignores persisted message', () => {
            expect(resolveDisplayMessage('New message', 'Stale persisted', null)).toBe('New message');
        });
    });

    describe('when realtime message is cleared (null)', () => {
        it('falls back to fallback message', () => {
            expect(resolveDisplayMessage(null, 'Persisted', 'Fallback')).toBe('Fallback');
        });

        it('returns null when no fallback configured', () => {
            expect(resolveDisplayMessage(null, 'Persisted', null)).toBeNull();
        });
    });

    describe('edge cases', () => {
        it('empty string realtime falls through to fallback', () => {
            expect(resolveDisplayMessage('', null, 'Fallback')).toBe('Fallback');
        });

        it('empty string persisted falls through to fallback', () => {
            expect(resolveDisplayMessage(undefined, '', 'Fallback')).toBe('Fallback');
        });

        it('all empty/null returns null', () => {
            expect(resolveDisplayMessage(undefined, null, null)).toBeNull();
        });
    });
});
