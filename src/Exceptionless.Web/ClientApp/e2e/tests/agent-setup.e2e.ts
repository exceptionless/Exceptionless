import type { Page } from '@playwright/test';

import { expect, test } from '../fixtures/e2e-test';
import { ExceptionlessE2EJourney } from '../support/exceptionless-journey';

test('new user can hand client setup to a coding agent @signup', async ({ e2eApi, page }, testInfo) => {
    const journey = new ExceptionlessE2EJourney(page, e2eApi, testInfo);
    await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);

    try {
        await test.step('create the first project', async () => {
            await journey.signUpAndCreateOrganization();
            await journey.createFirstProjectAndVerifyConfigureToken();
        });

        await test.step('before a platform is picked, the agent works out the stack', async () => {
            // Arrange
            await page.goto(`/project/${journey.projectId}/configure`);
            const row = page.locator('[data-agent-setup]');
            await expect(row).toContainText('Using a coding agent?');

            // Act
            await row.getByRole('button', { name: 'Set up with your agent' }).click();
            const links = await page
                .getByRole('menuitem')
                .and(page.locator('a'))
                .evaluateAll((elements) => elements.map((element) => [element.getAttribute('href'), element.getAttribute('target')]));
            await copyPrompt(page);
            const prompt = await readClipboard(page);

            // Assert
            expect(prompt).toMatch(
                new RegExp(
                    `^Fetch and follow the instructions at https://exceptionless\\.com/setup\\.md to set up Exceptionless for me\\. My API key is ${journey.projectToken}\\.`
                )
            );
            expect(links).toEqual([
                [`https://cursor.com/link/prompt?text=${encodeURIComponent(prompt)}`, '_blank'],
                [`https://claude.ai/new?q=${encodeURIComponent(prompt)}`, '_blank'],
                [`https://chatgpt.com/?q=${encodeURIComponent(prompt)}`, '_blank']
            ]);
        });

        await test.step('after a platform is picked, the prompt names it', async () => {
            // Arrange
            await page
                .getByRole('button', { name: /ASP\.NET Core/ })
                .first()
                .click();

            // Act
            await page.getByRole('region', { name: 'Connect ASP.NET Core' }).getByRole('button', { name: 'Set up with your agent' }).click();
            await copyPrompt(page);

            // Assert
            await expect(page.locator('[data-agent-setup]')).toHaveCount(0);
            expect(await readClipboard(page)).toContain('set up Exceptionless for me in this ASP.NET Core app.');
        });
    } finally {
        await journey.cleanup();
    }
});

async function copyPrompt(page: Page): Promise<void> {
    await page.getByRole('menuitem', { name: /Copy setup prompt/ }).click();
    await expect(page.getByText('Setup prompt copied. Paste it into your coding agent.').first()).toBeVisible();
}

function readClipboard(page: Page): Promise<string> {
    return page.evaluate(() => navigator.clipboard.readText());
}
