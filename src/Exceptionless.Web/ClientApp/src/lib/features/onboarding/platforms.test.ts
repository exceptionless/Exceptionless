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
        // Act
        const platform = findPlatform(type);

        // Assert
        expect(platform?.id).toBe(expectedId);
    });

    it.each(['dotnet-worker', 'javascript-react', 'javascript-vue', 'javascript-angular', 'curl'])('resolves the new %s type', (type) => {
        // Act
        const platform = findPlatform(type);

        // Assert
        expect(platform?.id).toBe(type);
    });

    it.each(['cobol', '', null])('ignores the unknown or empty type %j', (type) => {
        // Act
        const platform = findPlatform(type);

        // Assert
        expect(platform).toBeUndefined();
    });

    it('uses unique ids, aliases, and labels', () => {
        // Act
        const values = PLATFORMS.flatMap((platform) => [platform.id, ...(platform.aliases ?? [])]);
        const labels = PLATFORMS.map((platform) => platform.label);

        // Assert
        expect(new Set(values).size).toBe(values.length);
        expect(new Set(labels).size).toBe(PLATFORMS.length);
    });

    it('keeps the .NET Framework entries in the legacy group', () => {
        // Act
        const legacyIds = LEGACY_PLATFORMS.map((platform) => platform.id);
        const popularIds = POPULAR_PLATFORMS.map((platform) => platform.id);

        // Assert
        expect(legacyIds.every((id) => id.startsWith('dotnet-legacy-'))).toBe(true);
        expect(popularIds).toEqual([
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
        // Act
        const code = platform
            .steps(context)
            .flatMap((step) => step.blocks ?? [])
            .map((block) => block.code)
            .join('\n');

        // Assert
        expect(code).toContain('API_KEY_VALUE');
        expect(code).toContain(TEST_ERROR_MESSAGE);
        expect(code).not.toContain('--save');
        expect(code).not.toMatch(/^await Exceptionless\.startup/m);
        expect(code).not.toContain('AssemblyInfo');
    });

    it('adds the server URL to SDK snippets only when one should be shown', () => {
        // Arrange
        const aspNetCore = findPlatform('dotnet-aspnetcore')!;

        // Act
        const withServer = aspNetCore.steps({ ...context, sdkServerUrl: 'https://ex.example.com' })[1]!.blocks![0]!.code;
        const withoutServer = aspNetCore.steps(context)[1]!.blocks![0]!.code;

        // Assert
        expect(withServer).toContain('c.ServerUrl = "https://ex.example.com";');
        expect(withoutServer).not.toContain('ServerUrl');
    });

    it('always gives raw HTTP examples a submission URL', () => {
        // Arrange
        const bash = findPlatform('bash')!;

        // Act
        const code = bash.steps(context)[0]!.blocks![0]!.code;

        // Assert
        expect(code).toContain('https://ex.example.com/api/v2/events');
        expect(code).toContain('"type":"error"');
    });

    it.each([
        ['react', ['javascript-react', 'javascript-react-native', 'javascript-expo']],
        ['python', ['curl']],
        ['wpf framework', ['dotnet-legacy-wpf']]
    ])('finds platforms by label, category, and keyword: %s', (query, expectedIds) => {
        // Act
        const results = searchPlatforms(query);

        // Assert
        expect(results.map((platform) => platform.id)).toEqual(expectedIds);
    });

    it('lists every platform for a blank search', () => {
        // Act
        const results = searchPlatforms('  ');

        // Assert
        expect(results).toHaveLength(PLATFORMS.length);
    });
});
