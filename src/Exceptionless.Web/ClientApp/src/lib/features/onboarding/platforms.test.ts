import { describe, expect, it } from 'vitest';

import { findPlatform, LEGACY_PLATFORMS, PLATFORMS, POPULAR_PLATFORMS, searchPlatforms, TEST_ERROR_MESSAGE } from './platforms';

const context = { apiKey: 'API_KEY_VALUE', submissionServerUrl: 'https://ex.example.com' };

describe('client setup platforms', () => {
    // These values were offered by the previous project type dropdown and appear in shared links.
    it.each([
        ['bash', 'curl'],
        ['powershell', 'powershell'],
        ['dotnet-console', 'dotnet-console'],
        ['dotnet-aspnetcore', 'dotnet-aspnetcore'],
        ['dotnet-wpf', 'dotnet-wpf'],
        ['dotnet-winforms', 'dotnet-winforms'],
        ['javascript-browser', 'javascript-browser'],
        ['javascript-nodejs', 'javascript-nodejs'],
        ['javascript-react-native', 'javascript-react-native'],
        ['javascript-expo', 'javascript-expo'],
        ['dotnet-legacy-console', 'dotnet-legacy-console'],
        ['dotnet-legacy-mvc', 'dotnet-legacy-mvc'],
        ['dotnet-legacy-webapi', 'dotnet-legacy-webapi'],
        ['dotnet-legacy-webforms', 'dotnet-legacy-webforms'],
        ['dotnet-legacy-winforms', 'dotnet-legacy-winforms'],
        ['dotnet-legacy-wpf', 'dotnet-legacy-wpf']
    ])('keeps ?type=%s working', (type, expectedId) => {
        expect(findPlatform(type)?.id).toBe(expectedId);
    });

    it.each(['dotnet-worker', 'javascript-react', 'javascript-vue', 'javascript-angular', 'curl'])('resolves the new %s type', (type) => {
        expect(findPlatform(type)?.id).toBe(type);
    });

    it('ignores unknown and empty types', () => {
        expect(findPlatform('cobol')).toBeUndefined();
        expect(findPlatform('')).toBeUndefined();
        expect(findPlatform(null)).toBeUndefined();
    });

    it('uses unique ids, aliases, and labels', () => {
        const values = PLATFORMS.flatMap((platform) => [platform.id, ...(platform.aliases ?? [])]);
        expect(new Set(values).size).toBe(values.length);
        expect(new Set(PLATFORMS.map((platform) => platform.label)).size).toBe(PLATFORMS.length);
    });

    it('keeps the .NET Framework entries in the legacy group', () => {
        expect(LEGACY_PLATFORMS.map((platform) => platform.id).every((id) => id.startsWith('dotnet-legacy-'))).toBe(true);
        expect(POPULAR_PLATFORMS.map((platform) => platform.id)).toEqual([
            'dotnet-aspnetcore',
            'dotnet-console',
            'dotnet-worker',
            'javascript-browser',
            'javascript-nodejs',
            'javascript-react',
            'javascript-vue',
            'javascript-angular',
            'javascript-react-native',
            'javascript-expo',
            'dotnet-wpf',
            'dotnet-winforms',
            'curl',
            'powershell'
        ]);
    });

    it.each(PLATFORMS.map((platform) => [platform.id, platform] as const))('%s includes the API key and a test error', (_id, platform) => {
        const code = platform
            .steps(context)
            .flatMap((step) => step.blocks ?? [])
            .map((block) => block.code)
            .join('\n');

        expect(code).toContain('API_KEY_VALUE');
        expect(code).toContain(TEST_ERROR_MESSAGE);
        expect(code).not.toContain('--save');
        expect(code).not.toMatch(/^await Exceptionless\.startup/m);
        expect(code).not.toContain('AssemblyInfo');
    });

    it('adds the server URL to SDK snippets only when one should be shown', () => {
        const aspNetCore = findPlatform('dotnet-aspnetcore')!;
        const withServer = aspNetCore.steps({ ...context, sdkServerUrl: 'https://ex.example.com' })[1]!.blocks![0]!.code;
        const withoutServer = aspNetCore.steps(context)[1]!.blocks![0]!.code;

        expect(withServer).toContain('c.ServerUrl = "https://ex.example.com";');
        expect(withoutServer).not.toContain('ServerUrl');
    });

    it('always gives raw HTTP examples a submission URL', () => {
        const code = findPlatform('bash')!.steps(context)[0]!.blocks![0]!.code;
        expect(code).toContain('https://ex.example.com/api/v2/events');
        expect(code).toContain('"type":"error"');
    });

    it('finds platforms by label, category, and keyword', () => {
        expect(searchPlatforms('react').map((platform) => platform.id)).toEqual(['javascript-react', 'javascript-react-native', 'javascript-expo']);
        expect(searchPlatforms('python').map((platform) => platform.id)).toEqual(['curl']);
        expect(searchPlatforms('wpf framework').map((platform) => platform.id)).toEqual(['dotnet-legacy-wpf']);
        expect(searchPlatforms('  ')).toHaveLength(PLATFORMS.length);
    });
});
