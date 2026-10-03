import type { CreateQueryOptions, QueryClient as QueryClientType } from '@tanstack/svelte-query';

import * as Tooltip from '$comp/ui/tooltip';
import { organization } from '$features/organizations/context.svelte';
import { QueryClient } from '@tanstack/svelte-query';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { TagFilter } from './models.svelte';
import TagFacetedFilter from './tag-faceted-filter.svelte';

const mocks = vi.hoisted(() => ({
    client: undefined as QueryClientType | undefined,
    getJSON: vi.fn()
}));
vi.mock('$env/dynamic/public', () => ({ env: {} }));
vi.mock('$features/auth/index.svelte', () => ({ accessToken: { current: 'test-session' } }));
vi.mock('@foundatiofx/fetchclient', async (importOriginal) => ({
    ...(await importOriginal<typeof import('@foundatiofx/fetchclient')>()),
    useFetchClient: () => ({ getJSON: mocks.getJSON })
}));
vi.mock('@tanstack/svelte-query', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@tanstack/svelte-query')>();
    return { ...actual, createQuery: (options: () => CreateQueryOptions) => actual.createQuery(options, () => mocks.client!) };
});

describe('tag picker organization lifecycle', () => {
    beforeEach(() => {
        mocks.client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
        mocks.getJSON.mockResolvedValue({ data: { aggregations: {}, total: 0 } });
        organization.current = 'organization-a';
        vi.stubGlobal(
            'ResizeObserver',
            class {
                public disconnect() {}
                public observe() {}
                public unobserve() {}
            }
        );
        Element.prototype.scrollIntoView = vi.fn();
    });

    afterEach(() => {
        cleanup();
        mocks.client?.clear();
        vi.unstubAllGlobals();
    });

    it('keeps the outgoing picker scoped to its original organization until it is replaced', async () => {
        const props = { filter: new TagFilter(['Selected']), filterChanged: vi.fn(), filterRemoved: vi.fn(), open: true, title: 'Tag' };
        const picker = render(TagFacetedFilter, props, { wrapper: Tooltip.Provider });
        await waitFor(() => expect(mocks.getJSON).toHaveBeenCalledTimes(1));

        await fireEvent.input(screen.getByPlaceholderText('Tag'), { target: { value: 'rare' } });
        organization.current = 'organization-b';
        await tick();
        await new Promise((resolve) => setTimeout(resolve, 350));

        expect(mocks.getJSON).toHaveBeenCalledTimes(1);
        expect(props.filterChanged).not.toHaveBeenCalled();
        expect(props.filter.value).toEqual(['Selected']);

        picker.unmount();
        render(TagFacetedFilter, { ...props, filter: new TagFilter() }, { wrapper: Tooltip.Provider });
        await waitFor(() => expect(mocks.getJSON).toHaveBeenCalledTimes(2));
        expect(mocks.getJSON.mock.calls[1]![0]).toBe('/organizations/organization-b/events/count');
    });
});
