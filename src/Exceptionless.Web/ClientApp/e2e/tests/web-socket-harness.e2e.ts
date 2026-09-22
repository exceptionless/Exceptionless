import { expect, test } from '@playwright/test';

import { installWebSocketTestHarness } from '../support/web-socket';

test('aborting an SSE fetch unblocks the reader and removes its controller', async ({ page }) => {
    await installWebSocketTestHarness(page);
    await page.goto('about:blank');

    const abortResult = await page.evaluate(async () => {
        const abortController = new AbortController();
        const response = await fetch('http://localhost/api/v2/push', { signal: abortController.signal });
        const reader = response.body?.getReader();
        if (!reader) {
            throw new Error('Expected the SSE test response to have a body');
        }

        const read = reader.read().then(
            () => ({ status: 'resolved' as const }),
            (error: unknown) => ({
                name: error instanceof DOMException ? error.name : String(error),
                status: 'rejected' as const
            })
        );
        abortController.abort();
        return read;
    });

    expect(abortResult).toEqual({ name: 'AbortError', status: 'rejected' });
    await expect
        .poll(() =>
            page.evaluate(() => {
                const trackedWindow = window as Window & {
                    __exceptionlessE2ESseControllers?: unknown[];
                };
                return trackedWindow.__exceptionlessE2ESseControllers?.length ?? -1;
            })
        )
        .toBe(0);
});
