import { describe, expect, it } from 'vitest';

import { resolveClientSetupServerUrls } from './server-url';

describe('resolveClientSetupServerUrls', () => {
    it('uses the configured server URL', () => {
        expect(resolveClientSetupServerUrls({ configuredServerUrl: ' https://collector.example.com/ ', origin: 'https://app.example.com' })).toEqual({
            sdkServerUrl: 'https://collector.example.com',
            submissionServerUrl: 'https://collector.example.com'
        });
    });

    it('falls back to the API origin and then the app origin for self-hosted installs', () => {
        expect(resolveClientSetupServerUrls({ baseUrl: 'https://api.example.com/', configuredServerUrl: '', origin: 'https://app.example.com' })).toEqual({
            sdkServerUrl: 'https://api.example.com',
            submissionServerUrl: 'https://api.example.com'
        });
        expect(resolveClientSetupServerUrls({ baseUrl: '', origin: 'http://localhost:5000' })).toEqual({
            sdkServerUrl: 'http://localhost:5000',
            submissionServerUrl: 'http://localhost:5000'
        });
    });

    it('hides the server URL from SDK snippets only when the setting is false', () => {
        const hidden = resolveClientSetupServerUrls({
            configuredServerUrl: 'https://collector.example.com',
            origin: 'https://app.example.com',
            showServerUrl: 'false'
        });
        expect(hidden).toEqual({ sdkServerUrl: undefined, submissionServerUrl: 'https://collector.example.com' });

        const shown = resolveClientSetupServerUrls({ origin: 'https://app.example.com', showServerUrl: 'true' });
        expect(shown.sdkServerUrl).toBe('https://app.example.com');
    });

    it('ignores relative or invalid configured values', () => {
        expect(resolveClientSetupServerUrls({ baseUrl: '/api', configuredServerUrl: 'collector', origin: 'https://app.example.com' }).submissionServerUrl).toBe(
            'https://app.example.com'
        );
    });
});
