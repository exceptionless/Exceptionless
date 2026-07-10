import { defineConfig, devices } from '@playwright/test';

const isCi = !!process.env.CI;
const appUrl = process.env.E2E_URL || 'https://web-ex.dev.localhost:7131';

export default defineConfig({
    expect: {
        timeout: 10_000
    },

    outputDir: 'test-results',

    projects: [
        {
            name: 'chromium',
            use: {
                ...devices['Desktop Chrome']
            }
        }
    ],

    reporter: [['list'], ['html', { open: 'never' }], ['junit', { outputFile: 'test-results/e2e-junit-results.xml' }]],

    retries: isCi ? 2 : 0,

    testDir: 'e2e',

    testMatch: '**/*.e2e.{ts,js}',

    timeout: 120_000,

    use: {
        baseURL: appUrl,
        ignoreHTTPSErrors: true,
        screenshot: 'only-on-failure',
        trace: 'on-first-retry',
        video: 'retain-on-failure'
    },

    workers: 1
});
