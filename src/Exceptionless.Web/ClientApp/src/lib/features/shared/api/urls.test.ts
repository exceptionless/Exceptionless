import { beforeEach, describe, expect, it, vi } from 'vitest';

import { getApiUrl, getServerUrl } from './urls';

const { env } = vi.hoisted(() => ({ env: { PUBLIC_BASE_URL: '' } }));
vi.mock('$env/dynamic/public', () => ({ env }));

beforeEach(() => {
    vi.resetModules();
    env.PUBLIC_BASE_URL = '';
});

describe('API hosting URLs', () => {
    it('defaults to same-origin API, health, push and documentation endpoints', async () => {
        expect(getApiUrl()).toBe('/api/v2');
        expect(getApiUrl('/auth/login')).toBe('/api/v2/auth/login');
        expect(getServerUrl('health')).toBe('/health');
        expect(getServerUrl('/api/v2/push')).toBe('/api/v2/push');
        expect((await import('../help-links')).apiReferenceHref).toBe('/docs');
    });

    it('uses the configured API origin for ordinary, streaming, push, health and documentation requests', async () => {
        env.PUBLIC_BASE_URL = ' https://localhost:8443/ ';
        expect(getApiUrl()).toBe('https://localhost:8443/api/v2');
        expect(getApiUrl('auth/login')).toBe('https://localhost:8443/api/v2/auth/login');
        expect(getApiUrl('assistant/chat')).toBe('https://localhost:8443/api/v2/assistant/chat');
        expect(getServerUrl('/api/v2/push')).toBe('https://localhost:8443/api/v2/push');
        expect(getServerUrl('health')).toBe('https://localhost:8443/health');
        expect(getServerUrl('mcp')).toBe('https://localhost:8443/mcp');
        expect((await import('../help-links')).apiReferenceHref).toBe('https://localhost:8443/docs');
    });
});
