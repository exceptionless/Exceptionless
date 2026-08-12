import { describe, expect, it, vi } from 'vitest';

import { createCustomFieldsQueryOptions, CUSTOM_FIELD_QUERY_STALE_TIME_MS, queryKeys } from './api.svelte';

const fetchClientMocks = vi.hoisted(() => ({ getJSON: vi.fn() }));

vi.mock('@foundatiofx/fetchclient', () => ({
    useFetchClient: () => ({ getJSON: fetchClientMocks.getJSON })
}));

describe('createCustomFieldsQueryOptions', () => {
    it('keeps a stable cacheable request when the route changes', async () => {
        // Arrange
        const request = { route: { organizationId: 'organization-id' as string | undefined } };
        fetchClientMocks.getJSON.mockResolvedValueOnce({ data: [] });
        const options = createCustomFieldsQueryOptions(request);

        // Act
        const resultPromise = options.queryFn();
        request.route.organizationId = 'other-organization-id';
        const result = await resultPromise;

        // Assert
        expect(fetchClientMocks.getJSON).toHaveBeenCalledWith('organizations/organization-id/event-custom-fields');
        expect(options.queryKey).toEqual(queryKeys.customFields('organization-id'));
        expect(options.staleTime).toBe(CUSTOM_FIELD_QUERY_STALE_TIME_MS);
        expect(result).toEqual([]);
    });
});
