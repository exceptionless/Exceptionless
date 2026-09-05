import { describe, expect, it } from 'vitest';

import { clearProductTourSession, readProductTourSession, writeProductTourSession } from './session';

const unavailable = {
    getItem() {
        throw new DOMException('Denied', 'SecurityError');
    },
    removeItem() {
        throw new DOMException('Denied', 'SecurityError');
    },
    setItem() {
        throw new DOMException('Full', 'QuotaExceededError');
    }
};

describe('product-tour session persistence', () => {
    it('tolerates unavailable storage on read, write, and clear', () => {
        // Arrange
        const checkpoint = { checkpointName: 'navigation', source: 'catalog', tourName: 'app-overview', userId: 'user', version: 1 } as const;

        // Act & Assert
        expect(readProductTourSession(unavailable)).toBeUndefined();
        expect(() => writeProductTourSession(checkpoint, unavailable)).not.toThrow();
        expect(() => clearProductTourSession(unavailable)).not.toThrow();
    });

    it('round-trips only the current functional checkpoint', () => {
        // Arrange
        const checkpoint = {
            checkpointName: 'navigation',
            source: 'catalog',
            tourName: 'app-overview',
            userId: 'user',
            version: 1
        } as const;
        let value: null | string = null;
        const storage = {
            getItem: () => value,
            removeItem: () => {
                value = null;
            },
            setItem: (_key: string, next: string) => {
                value = next;
            }
        };

        // Act
        writeProductTourSession(checkpoint, storage);

        // Assert
        expect(readProductTourSession(storage)).toEqual(checkpoint);
    });
});
