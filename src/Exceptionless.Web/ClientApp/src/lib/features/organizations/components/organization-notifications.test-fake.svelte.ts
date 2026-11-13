import type { ViewOrganization } from '#features/organizations/models.ts';

/**
 * Reactive current user and organization for organization notification tests, so mocked queries
 * re-render the component when a test changes them.
 */
export const notificationTestState = $state({
    isRecordingProductTour: false,
    me: {
        id: 'user-id',
        organization_ids: ['organization-id'],
        product_tours: {} as Record<string, string>,
        roles: [] as string[]
    },
    organization: undefined as undefined | ViewOrganization
});
