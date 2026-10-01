import { beforeEach, describe, expect, it, vi } from 'vitest';

const { env } = vi.hoisted(() => ({ env: { PUBLIC_BASE_URL: '' } }));
vi.mock('$env/dynamic/public', () => ({ env }));

import { getApiUrl } from './url';

describe('API destinations', () => {
    beforeEach(() => {
        env.PUBLIC_BASE_URL = '';
    });

    it.each(['/api/v2', '/health', '/docs', '/api/v2/assistant/chat', '/api/v2/push'])('uses the app origin by default: %s', (path) => {
        expect(getApiUrl(path)).toBe(path);
    });

    it.each(['https://api.localhost:7111', ' https://api.localhost:7111/ ', 'https://api.localhost:7111///'])(
        'respects the configured separate API host: %s',
        (baseUrl) => {
            env.PUBLIC_BASE_URL = baseUrl;
            expect(getApiUrl('/api/v2')).toBe('https://api.localhost:7111/api/v2');
            expect(getApiUrl('docs')).toBe('https://api.localhost:7111/docs');
            expect(getApiUrl('/health')).toBe('https://api.localhost:7111/health');
        }
    );
});
