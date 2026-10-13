import { afterEach, expect, it, vi } from 'vitest';

import { fetchApiJson } from './api.svelte';

vi.mock('$env/dynamic/public', () => ({ env: { PUBLIC_BASE_URL: 'https://localhost:8443/' } }));
vi.mock('$features/auth/index.svelte', () => ({ accessToken: { current: 'existing-session' } }));

afterEach(() => vi.unstubAllGlobals());

it('sends authenticated direct requests to the configured API origin', async () => {
    const fetch = vi.fn<typeof globalThis.fetch>().mockResolvedValue(Response.json({ recorded: true }));
    vi.stubGlobal('fetch', fetch);

    expect(await fetchApiJson('users/me/product-tours/welcome/record', { method: 'PUT' })).toEqual({ recorded: true });
    const [url, init] = fetch.mock.calls[0]!;
    expect(url).toBe('https://localhost:8443/api/v2/users/me/product-tours/welcome/record');
    expect(init?.method).toBe('PUT');
    expect(new Headers(init?.headers).get('Authorization')).toBe('Bearer existing-session');
});
