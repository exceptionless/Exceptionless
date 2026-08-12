import { describe, expect, it, vi } from 'vitest';

import { clearProductTourSession, readProductTourSession, writeProductTourSession } from './session';

describe('product tour session', () => {
    it('round-trips stable resume state', () => {
        let value: null | string = null;
        const storage = {
            getItem: () => value,
            removeItem: vi.fn(() => (value = null)),
            setItem: vi.fn((_key: string, next: string) => (value = next))
        };
        const session = { source: 'command-palette', stepId: 'choose-error', tourId: 'investigate-error', version: 1 } as const;

        writeProductTourSession(session, storage);
        expect(readProductTourSession(storage)).toEqual(session);
        clearProductTourSession(storage);
        expect(value).toBeNull();
    });

    it('clears malformed state instead of blocking future guides', () => {
        const removeItem = vi.fn();
        const storage = { getItem: () => '{not-json', removeItem };

        expect(readProductTourSession(storage)).toBeUndefined();
        expect(removeItem).toHaveBeenCalledOnce();
    });
});
