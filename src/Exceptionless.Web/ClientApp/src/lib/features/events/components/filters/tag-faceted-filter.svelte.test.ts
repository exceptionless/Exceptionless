import { organization } from '$features/organizations/context.svelte';
import { render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, expect, it, vi } from 'vitest';

import TagFacetedFilter from './tag-faceted-filter.test-harness.svelte';

vi.mock('$features/events/api.svelte', () => ({
    getOrganizationCountQuery: (request: { route: { organizationId: string | undefined } }) => ({
        get data() {
            return { aggregations: { terms_tags: { items: [{ key: `${request.route.organizationId}-tag`, total: 1 }] } } };
        },
        isLoading: false,
        isSuccess: true
    })
}));

afterEach(() => {
    organization.current = undefined;
});

it('updates the open tag picker when the current organization changes', async () => {
    organization.current = 'membership';
    render(TagFacetedFilter);
    await waitFor(() => expect(screen.getByRole('option', { name: 'membership-tag' })).toBeTruthy());

    organization.current = 'impersonated';

    await waitFor(() => expect(screen.getByRole('option', { name: 'impersonated-tag' })).toBeTruthy());
    expect(screen.queryByRole('option', { name: 'membership-tag' })).toBeNull();
});
