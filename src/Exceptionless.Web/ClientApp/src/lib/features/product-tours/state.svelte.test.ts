import { describe, expect, it } from 'vitest';

import { productTourHost } from './state.svelte';

describe('product tour host', () => {
    it('waits for completion listeners before resolving', async () => {
        let finishPersistence: () => void = () => undefined;
        const persistence = new Promise<void>((resolve) => (finishPersistence = resolve));
        let persisted = false;
        const unsubscribe = productTourHost.subscribe(async () => {
            await persistence;
            persisted = true;
        });

        try {
            const completion = productTourHost.complete('configure-project');
            await Promise.resolve();
            expect(persisted).toBe(false);

            finishPersistence();
            await completion;
            expect(persisted).toBe(true);
        } finally {
            unsubscribe();
        }
    });
});
