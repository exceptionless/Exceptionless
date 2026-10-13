import { accessToken } from '$features/auth/state.svelte';
import { QueryClient } from '@tanstack/svelte-query';
import { cleanup, render } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, expect, it, vi } from 'vitest';

import QueryGateTestHarness from './query-gating.test-harness.svelte';

const mocks = vi.hoisted(() => ({ getJSON: vi.fn() }));
vi.mock('$env/dynamic/public', () => ({ env: {} }));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@foundatiofx/fetchclient')>()),
    useFetchClient: () => ({ getJSON: mocks.getJSON })
}));

beforeEach(() => {
    accessToken.current = 'test-token';
});

afterEach(() => {
    cleanup();
    accessToken.current = null;
    vi.clearAllMocks();
});

it.each([
    {
        data: { default_enabled: true, enabled: true, is_overridden: false },
        gate: 'assistant-sharing' as const,
        path: 'assistant/conversation-sharing'
    },
    {
        data: [{ id: 'project-id', name: 'Example project' }],
        gate: 'project-commands' as const,
        path: 'organizations/organization-id/projects'
    }
])('$gate loads when opened after mounting disabled', async ({ data, gate, path }) => {
    // Arrange: keep identifiers fixed so only opening the feature can enable its read.
    const client = new QueryClient({ defaultOptions: { queries: { gcTime: 0, retry: false } } });
    const response = { data, ok: true };
    mocks.getJSON.mockResolvedValue(response);
    const view = render(QueryGateTestHarness, { client, enabled: false, gate });
    await tick();
    expect(mocks.getJSON).not.toHaveBeenCalled();

    // Act
    await view.rerender({ client, enabled: true, gate });

    // Assert
    await vi.waitFor(() => expect(mocks.getJSON).toHaveBeenCalledOnce());
    expect(mocks.getJSON).toHaveBeenCalledWith(path, expect.objectContaining({ signal: expect.any(AbortSignal) }));
    await vi.waitFor(() => expect(view.container.querySelector('output')?.textContent).toBe(JSON.stringify(gate === 'project-commands' ? response : data)));
    client.clear();
});
