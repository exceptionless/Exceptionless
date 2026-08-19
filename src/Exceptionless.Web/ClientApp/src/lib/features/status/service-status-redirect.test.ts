import { describe, expect, it, vi } from 'vitest';

import { buildServiceStatusUrl, createServiceStatusRedirector, isServiceUnavailableStatus } from './service-status-redirect';

describe('isServiceUnavailableStatus', () => {
    it.each([0, 408, 500, 503, 599])('treats %s as service unavailable', (status) => {
        expect(isServiceUnavailableStatus(status)).toBe(true);
    });

    it.each([200, 400, 404, 429])('does not treat %s as service unavailable', (status) => {
        expect(isServiceUnavailableStatus(status)).toBe(false);
    });
});

describe('buildServiceStatusUrl', () => {
    it('preserves the current path, query, and hash as an encoded redirect', () => {
        const url = new URL('https://example.test/next/stack/most-frequent-errors?project=project-1&filter=status%3Aopen#details');

        const result = buildServiceStatusUrl('/next/status', url);

        expect(new URL(result, url.origin).searchParams.get('redirect')).toBe(
            '/next/stack/most-frequent-errors?project=project-1&filter=status%3Aopen#details'
        );
    });
});

describe('createServiceStatusRedirector', () => {
    it('coalesces concurrent health checks and stays on the current page when the service is healthy', async () => {
        let resolveHealth!: (value: boolean) => void;
        const healthResult = new Promise<boolean>((resolve) => {
            resolveHealth = resolve;
        });
        const checkHealth = vi.fn(() => healthResult);
        const navigate = vi.fn(async () => undefined);
        const redirect = createServiceStatusRedirector({ checkHealth, navigate });

        const first = redirect();
        const second = redirect();
        resolveHealth(true);
        await Promise.all([first, second]);

        expect(checkHealth).toHaveBeenCalledOnce();
        expect(navigate).not.toHaveBeenCalled();
    });

    it('briefly caches a healthy result to bound repeated probes', async () => {
        let currentTime = 1000;
        const checkHealth = vi.fn(async () => true);
        const redirect = createServiceStatusRedirector({
            checkHealth,
            healthyCacheMilliseconds: 5000,
            navigate: vi.fn(async () => undefined),
            now: () => currentTime
        });

        await redirect();
        currentTime = 5999;
        await redirect();
        currentTime = 6000;
        await redirect();

        expect(checkHealth).toHaveBeenCalledTimes(2);
    });

    it('coalesces navigation when the service is unavailable', async () => {
        let resolveHealth!: (value: boolean) => void;
        let resolveNavigation!: () => void;
        const healthResult = new Promise<boolean>((resolve) => {
            resolveHealth = resolve;
        });
        const navigationResult = new Promise<void>((resolve) => {
            resolveNavigation = resolve;
        });
        const checkHealth = vi.fn(() => healthResult);
        const navigate = vi.fn(() => navigationResult);
        const redirect = createServiceStatusRedirector({ checkHealth, navigate });

        const first = redirect();
        const second = redirect();
        resolveHealth(false);
        await vi.waitFor(() => expect(navigate).toHaveBeenCalledOnce());
        resolveNavigation();
        await Promise.all([first, second]);

        expect(checkHealth).toHaveBeenCalledOnce();
        expect(navigate).toHaveBeenCalledOnce();
    });

    it('treats a failed health probe as unavailable', async () => {
        const navigate = vi.fn(async () => undefined);
        const redirect = createServiceStatusRedirector({
            checkHealth: vi.fn(async () => {
                throw new Error('network unavailable');
            }),
            navigate
        });

        await redirect();

        expect(navigate).toHaveBeenCalledOnce();
    });
});
