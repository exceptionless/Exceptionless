import { describe, expect, it } from 'vitest';

import { AGENT_SETUP_URL, buildAgentSetupMarkdown, buildAgentSetupPrompt, getAgentUrl } from './agent-setup';
import { findPlatform, PLATFORMS, TEST_ERROR_MESSAGE } from './platforms';

const context = { apiKey: 'API_KEY_VALUE', submissionServerUrl: 'https://ex.example.com' };

describe('agent setup', () => {
    it.each([
        [
            'no platform on the hosted service',
            context,
            undefined,
            `Fetch and follow the instructions at ${AGENT_SETUP_URL} to set up Exceptionless for me. My API key is API_KEY_VALUE.`
        ],
        [
            'a chosen platform on a self-hosted server',
            { ...context, sdkServerUrl: 'https://ex.example.com' },
            'dotnet-aspnetcore',
            `Fetch and follow the instructions at ${AGENT_SETUP_URL} to set up Exceptionless for me in this ASP.NET Core app. My API key is API_KEY_VALUE. My Exceptionless server is https://ex.example.com.`
        ]
    ])('points the agent at the published instructions for %s', (_scenario, setupContext, platformId, expected) => {
        // Arrange
        const platform = platformId ? findPlatform(platformId) : undefined;

        // Act
        const prompt = buildAgentSetupPrompt(setupContext, platform);

        // Assert
        expect(prompt).toBe(expected);
    });

    it.each([
        ['cursor', 'https://cursor.com/link/prompt?text='],
        ['claude', 'https://claude.ai/new?q='],
        ['chatgpt', 'https://chatgpt.com/?q=']
    ] as const)('opens %s with the prompt filled in', (target, prefix) => {
        // Arrange
        const prompt = 'Set up Exceptionless & send "a test"';

        // Act
        const url = getAgentUrl(target, prompt);

        // Assert
        expect(url).toBe(`${prefix}Set%20up%20Exceptionless%20%26%20send%20%22a%20test%22`);
    });

    it('covers every platform with a placeholder API key and the test error', () => {
        // Arrange
        const headings = PLATFORMS.map((platform) => `\n## ${platform.label}\n`);

        // Act
        const markdown = buildAgentSetupMarkdown();

        // Assert
        expect(headings.filter((heading) => !markdown.includes(heading))).toEqual([]);
        expect(markdown).toContain('YOUR_API_KEY');
        expect(markdown).toContain(TEST_ERROR_MESSAGE);
        expect(markdown).toContain('```bash\ndotnet add package Exceptionless.AspNetCore\n```');
    });

    // The website publishes docs/public/setup.md.txt at AGENT_SETUP_URL. Run `npx vitest run -u` after changing platform steps.
    it('matches the published setup instructions', async () => {
        // Arrange
        const publishedPath = '../../../../../../../docs/public/setup.md.txt';

        // Act
        const markdown = `${buildAgentSetupMarkdown()}\n`;

        // Assert
        await expect(markdown).toMatchFileSnapshot(publishedPath);
    });
});
