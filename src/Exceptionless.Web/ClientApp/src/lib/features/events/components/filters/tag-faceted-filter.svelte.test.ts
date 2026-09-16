import { render } from '@testing-library/svelte';
import { tick } from 'svelte';
import { describe, expect, it, vi } from 'vitest';

import { TagFilter } from './models.svelte';
import TagFacetedFilter from './tag-faceted-filter.svelte';

vi.mock('$features/organizations/context.svelte', () => ({ organization: { current: 'organization-id' } }));
vi.mock('$features/events/api.svelte', () => ({
    getTagSuggestionsQuery: () => ({
        data: { aggregations: { terms_tags: { data: { '@type': 'bucket', SumOtherDocCount: 1 }, items: [{ key: 'common', total: 2 }] } } },
        isError: false,
        isFetching: false,
        isSuccess: true
    })
}));

describe('tag suggestions', () => {
    it('preserves a selected tag absent from the returned aggregation', async () => {
        const filter = new TagFilter(['rare']);
        const filterChanged = vi.fn();
        render(TagFacetedFilter, { filter, filterChanged, filterRemoved: vi.fn(), open: false, title: 'Tag' });
        await tick();
        expect(filter.value).toEqual(['rare']);
        expect(filterChanged).not.toHaveBeenCalled();
    });
});
