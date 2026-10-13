import { defineConfig } from '@playwright/test';

export default defineConfig({
    reporter: [
        ['html', { open: 'never' }],
        ['json', { outputFile: 'test-results/e2e-results.json' }],
        ['junit', { outputFile: 'test-results/e2e-junit-results.xml' }]
    ],
    testDir: 'e2e'
});
