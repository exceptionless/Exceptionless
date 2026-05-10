import { test as base, expect, type TestInfo } from '@playwright/test';

import { E2EApiClient } from './api-client';
import { getE2EEnvironment } from './environment';

interface E2EFixtures {
    e2eApi: E2EApiClient;
}

export const test = base.extend<E2EFixtures>({
    e2eApi: async ({ request }, use) => {
        await use(new E2EApiClient(request, getE2EEnvironment()));
    }
});

export { expect };

export function createRunName(runId: string, testInfo: TestInfo): string {
    const rawName = [runId, `w${testInfo.workerIndex}`, `r${testInfo.retry}`, Date.now().toString(36)].join('-');
    return rawName
        .replace(/[^a-zA-Z0-9_-]/g, '-')
        .replace(/-+/g, '-')
        .slice(0, 96);
}
