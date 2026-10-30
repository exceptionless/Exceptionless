import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';

test.use({ e2eUseGeneratedUser: true });

test('AI tool setup links select the requested tool and Cursor offers an install link', async ({ browser, e2eApi, e2eScenario, page }) => {
    await page.context().grantPermissions(['clipboard-read', 'clipboard-write']);
    await page.goto('/account/ai-tools?tool=cursor&source=shared');

    const toolSelector = page.getByRole('combobox', { name: 'AI tool' });
    await expect(toolSelector).toHaveText('Cursor');
    await expect(page.getByText('Connect to the server in Cursor and approve the OAuth sign-in when prompted.')).toBeVisible();
    await page.setViewportSize({ height: 844, width: 390 });
    await expect(toolSelector).toBeVisible();
    await expect(page.getByRole('button', { name: 'Copy link' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Add to Cursor' })).toBeVisible();
    await page.setViewportSize({ height: 720, width: 1280 });

    const configuration = JSON.parse(await page.locator('pre').first().innerText()) as { mcpServers: Record<string, { url: string }> };
    const serverNames = Object.keys(configuration.mcpServers);
    expect(serverNames).toHaveLength(1);
    const serverName = serverNames[0]!;
    const server = configuration.mcpServers[serverName]!;
    expect(server.url).toBe(new URL('/mcp', page.url()).href);

    const installLink = new URL((await page.getByRole('link', { name: 'Add to Cursor' }).getAttribute('href'))!);
    expect(`${installLink.protocol}//${installLink.host}${installLink.pathname}`).toBe('cursor://anysphere.cursor-deeplink/mcp/install');
    expect(installLink.searchParams.get('name')).toBe(serverName);
    expect(JSON.parse(Buffer.from(installLink.searchParams.get('config')!, 'base64').toString('utf8'))).toEqual(server);

    await toolSelector.click();
    await page.getByRole('option', { exact: true, name: 'Codex' }).click();
    await expect.poll(() => new URL(page.url()).searchParams.get('source')).toBe('shared');
    await expect.poll(() => new URL(page.url()).searchParams.get('tool')).toBe('codex');
    await page.goBack();
    await expect(toolSelector).toHaveText('Cursor');

    await page.getByRole('button', { name: 'Copy link' }).click();
    const copiedLink = await page.evaluate(() => navigator.clipboard.readText());
    expect(copiedLink).toBe(new URL('/account/ai-tools?tool=cursor', page.url()).href);
    await page.goto(copiedLink);
    await expect(toolSelector).toHaveText('Cursor');

    await toolSelector.click();
    await page.getByRole('option', { exact: true, name: 'Codex' }).click();
    await expect(page).toHaveURL(/\?tool=codex/);
    await expect(page.getByText(/codex mcp add/)).toBeVisible();
    await page.reload();
    await expect(toolSelector).toHaveText('Codex');
    await page.goBack();
    await expect(toolSelector).toHaveText('Cursor');
    await page.goForward();
    await expect(toolSelector).toHaveText('Codex');

    for (const [id, name] of [
        ['claude', 'Claude Code'],
        ['codex', 'Codex'],
        ['cursor', 'Cursor'],
        ['github-copilot', 'Copilot'],
        ['opencode', 'OpenCode'],
        ['unknown', 'Claude Code']
    ]) {
        await page.goto(`/account/ai-tools?tool=${id}`);
        await expect(toolSelector).toHaveText(name!);
    }

    const signedOutContext = await browser.newContext({ baseURL: e2eApi.environment.appUrl, ignoreHTTPSErrors: true });
    try {
        const signedOutPage = await signedOutContext.newPage();
        await signedOutPage.goto(copiedLink);
        await expect(signedOutPage.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
        await expect.poll(() => new URL(signedOutPage.url()).searchParams.get('redirect')).toBe('/account/ai-tools?tool=cursor');
        await signedOutPage.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
        await signedOutPage.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
        await signedOutPage.getByRole('button', { exact: true, name: 'Login' }).click();
        await expect(signedOutPage).toHaveURL(copiedLink);
        await expect(signedOutPage.getByRole('combobox', { name: 'AI tool' })).toHaveText('Cursor');
    } finally {
        await signedOutContext.close();
    }
});
