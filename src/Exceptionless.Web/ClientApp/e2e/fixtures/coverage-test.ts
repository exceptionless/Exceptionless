import { test as base } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';

import { BrowserCoverage } from '../../scripts/test-coverage/browser';

export const test = base.extend({
    browser: async ({ browser }, use, workerInfo) => {
        const output = process.env.E2E_FRONTEND_COVERAGE_DIRECTORY;
        if (!output) {
            await use(browser);
            return;
        }

        const hostname = new URL(process.env.E2E_URL ?? '').hostname;
        if (hostname !== 'localhost' && hostname !== '127.0.0.1' && !hostname.endsWith('.localhost')) {
            throw new Error('Frontend coverage must use a localhost application');
        }
        if (browser.browserType().name() !== 'chromium') {
            throw new Error('Frontend coverage currently requires Chromium');
        }

        const collector = new BrowserCoverage();
        await collector.install(browser);
        const errors: unknown[] = [];
        try {
            await use(browser);
        } catch (error) {
            errors.push(error);
        }
        try {
            const result = await collector.finish(browser);
            writeFileSync(
                join(output, `worker-${workerInfo.workerIndex}-${randomUUID()}.json`),
                JSON.stringify({ ...result, session: process.env.E2E_RUN_ID }) + '\n'
            );
            if (!result.complete) errors.push(new Error(`Frontend coverage incomplete: ${result.errors.join('; ')}`));
        } catch (error) {
            errors.push(error);
        }
        if (errors.length) throw new AggregateError(errors, 'Browser execution or coverage collection failed');
    }
});
