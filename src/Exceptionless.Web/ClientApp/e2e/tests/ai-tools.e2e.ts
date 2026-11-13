import { E2E_TEST_PASSWORD, expect, test } from '../fixtures/e2e-test';

test.use({ e2eUseGeneratedUser: true });

test('AI tool setup links select the requested tool and supported editors offer install links', async ({ browser, e2eApi, e2eScenario, page }) => {
    await page.goto('/account/ai-tools?type=cursor&source=shared');

    const toolSelector = page.getByRole('combobox', { name: 'AI tool' });
    await expect(toolSelector).toHaveText('Cursor');
    await expect(page.getByText('Connect to the server in Cursor and approve the OAuth sign-in when prompted.')).toBeVisible();
    await page.setViewportSize({ height: 844, width: 390 });
    await expect(toolSelector).toBeVisible();
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
    await expect.poll(() => new URL(page.url()).searchParams.get('type')).toBe('codex');
    await page.goBack();
    await expect(toolSelector).toHaveText('Cursor');

    const shareableLink = page.url();
    expect(new URL(shareableLink).searchParams.get('type')).toBe('cursor');
    await page.goto(shareableLink);
    await expect(toolSelector).toHaveText('Cursor');

    await toolSelector.click();
    await page.getByRole('option', { exact: true, name: 'Codex' }).click();
    await expect(page).toHaveURL(/\?type=codex/);
    await expect(page.getByText(/codex mcp add/)).toBeVisible();
    await page.reload();
    await expect(toolSelector).toHaveText('Codex');
    await page.goBack();
    await expect(toolSelector).toHaveText('Cursor');
    await page.goForward();
    await expect(toolSelector).toHaveText('Codex');

    await toolSelector.click();
    await page.getByRole('option', { exact: true, name: 'VS Code' }).click();
    await expect.poll(() => new URL(page.url()).searchParams.get('type')).toBe('vscode');
    await page.reload();
    await expect(toolSelector).toHaveText('VS Code');
    await expect(page.getByText('Connect to the server in VS Code and approve the OAuth sign-in when prompted.')).toBeVisible();
    const vscodeConfiguration = JSON.parse(await page.locator('pre').first().innerText()) as {
        servers: Record<string, { type: string; url: string }>;
    };
    expect(vscodeConfiguration.servers).toEqual({ [serverName]: { type: 'http', url: server.url } });
    const vscodeInstallLink = await page.getByRole('link', { name: 'Add to VS Code' }).getAttribute('href');
    expect(vscodeInstallLink).toMatch(/^vscode:mcp\/install\?/);
    expect(JSON.parse(decodeURIComponent(vscodeInstallLink!.split('?')[1]!))).toEqual({ name: serverName, type: 'http', url: server.url });

    for (const [id, name] of [
        ['claude', 'Claude Code'],
        ['codex', 'Codex'],
        ['cursor', 'Cursor'],
        ['github-copilot', 'Copilot'],
        ['opencode', 'OpenCode'],
        ['vscode', 'VS Code'],
        ['unknown', 'Claude Code']
    ]) {
        await page.goto(`/account/ai-tools?type=${id}`);
        await expect(toolSelector).toHaveText(name!);
    }

    const signedOutContext = await browser.newContext({ baseURL: e2eApi.environment.appUrl, ignoreHTTPSErrors: true });
    try {
        const signedOutPage = await signedOutContext.newPage();
        await signedOutPage.goto(shareableLink);
        await expect(signedOutPage.getByRole('button', { exact: true, name: 'Login' })).toBeVisible();
        await expect.poll(() => new URL(signedOutPage.url()).searchParams.get('redirect')).toBe('/account/ai-tools?type=cursor&source=shared');
        await signedOutPage.getByLabel('Email', { exact: true }).fill(e2eScenario.email);
        await signedOutPage.getByPlaceholder('Enter password').fill(E2E_TEST_PASSWORD);
        await signedOutPage.getByRole('button', { exact: true, name: 'Login' }).click();
        await expect(signedOutPage).toHaveURL(shareableLink);
        await expect(signedOutPage.getByRole('combobox', { name: 'AI tool' })).toHaveText('Cursor');
    } finally {
        await signedOutContext.close();
    }
});
