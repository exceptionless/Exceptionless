import { describe, expect, it } from 'vitest';

import { resolveClientSetupServerUrls } from './server-url';

describe('resolveClientSetupServerUrls', () => {
    it('uses the configured server URL', () => {
        // Arrange
        const input = { configuredServerUrl: ' https://collector.example.com/ ', origin: 'https://app.example.com' };

        // Act
        const urls = resolveClientSetupServerUrls(input);

        // Assert
        expect(urls).toEqual({
            sdkServerUrl: 'https://collector.example.com',
            submissionServerUrl: 'https://collector.example.com'
        });
    });

    it.each([
        [{ baseUrl: 'https://api.example.com/', configuredServerUrl: '', origin: 'https://app.example.com' }, 'https://api.example.com'],
        [{ baseUrl: '', origin: 'http://localhost:5000' }, 'http://localhost:5000']
    ])('falls back to the API origin and then the app origin for self-hosted installs: %j', (input, expected) => {
        // Act
        const urls = resolveClientSetupServerUrls(input);

        // Assert
        expect(urls).toEqual({ sdkServerUrl: expected, submissionServerUrl: expected });
    });

    it('hides the server URL from SDK snippets when the setting is false', () => {
        // Arrange
        const input = { configuredServerUrl: 'https://collector.example.com', origin: 'https://app.example.com', showServerUrl: 'false' };

        // Act
        const urls = resolveClientSetupServerUrls(input);

        // Assert
        expect(urls).toEqual({ sdkServerUrl: undefined, submissionServerUrl: 'https://collector.example.com' });
    });

    it('shows the server URL in SDK snippets when the setting is true', () => {
        // Arrange
        const input = { origin: 'https://app.example.com', showServerUrl: 'true' };

        // Act
        const urls = resolveClientSetupServerUrls(input);

        // Assert
        expect(urls.sdkServerUrl).toBe('https://app.example.com');
    });

    it('ignores relative or invalid configured values', () => {
        // Arrange
        const input = { baseUrl: '/api', configuredServerUrl: 'collector', origin: 'https://app.example.com' };

        // Act
        const urls = resolveClientSetupServerUrls(input);

        // Assert
        expect(urls.submissionServerUrl).toBe('https://app.example.com');
    });
});
