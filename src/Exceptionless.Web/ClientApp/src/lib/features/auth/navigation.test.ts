import { beforeEach, describe, expect, it, vi } from 'vitest';

const { goto, page, paths } = vi.hoisted(() => ({
    goto: vi.fn(),
    page: { url: new URL('https://localhost/next/') },
    paths: { base: '/next' }
}));

vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => `${paths.base}${path.replace('/(auth)', '')}` }));
vi.mock('$app/state', () => ({ page }));
vi.mock('$env/dynamic/public', () => ({ env: {} }));
vi.mock('./api.svelte', () => ({}));
vi.mock('./state.svelte', () => ({ accessToken: { current: null } }));
vi.mock('./validators', () => ({ validateEmailAvailability: vi.fn() }));

import { gotoLogin } from './index.svelte';

describe('gotoLogin', () => {
    beforeEach(() => {
        goto.mockReset();
        paths.base = '/next';
    });

    it.each([
        '/next/account/verify?token=verification%2Btoken%26value',
        '/next/account/notifications?project=project-id&from=email#project-notifications',
        '/next/organization/organization-id/billing?changePlan=true#billing',
        '/next/event?filter=message%3A%22A%26B%22&tag=one&tag=two#details',
        '/next/stack/stack-id'
    ])('preserves the complete return destination %s', async (destination) => {
        page.url = new URL(destination, 'https://localhost');

        await gotoLogin();

        expect(goto).toHaveBeenCalledExactlyOnceWith(`/next/login?redirect=${encodeURIComponent(destination)}`, { replaceState: true });
        const loginUrl = new URL(goto.mock.calls[0]![0], page.url.origin);
        expect(loginUrl.searchParams.get('redirect')).toBe(destination);
        expect([...loginUrl.searchParams.keys()]).toEqual(['redirect']);
        expect(loginUrl.hash).toBe('');
    });

    it.each(['/next/', '/next/login', '/next/login/'])('does not nest a login redirect from %s', async (path) => {
        page.url = new URL(path, 'https://localhost');

        await gotoLogin();

        expect(goto).toHaveBeenCalledExactlyOnceWith('/next/login', { replaceState: true });
    });

    it('uses the configured login route when the app is hosted at root', async () => {
        paths.base = '';
        page.url = new URL('https://localhost/login');

        await gotoLogin();

        expect(goto).toHaveBeenCalledExactlyOnceWith('/login', { replaceState: true });
    });
});
