import { beforeEach, describe, expect, it, vi } from 'vitest';

import { createProductTourActivity } from './api.svelte';

const mocks = vi.hoisted(() => ({ getQueryData: vi.fn(), postJSON: vi.fn() }));
vi.mock('$features/users/api.svelte', () => ({ queryKeys: { me: () => ['User', 'me'] } }));
vi.mock('@foundatiofx/fetchclient', () => ({ useFetchClient: () => ({ postJSON: mocks.postJSON }) }));
vi.mock('@tanstack/svelte-query', () => ({
    createMutation: (options: () => { mutationFn: (data: unknown) => Promise<void> }) => ({ mutateAsync: (data: unknown) => options().mutationFn(data) }),
    useQueryClient: () => ({ getQueryData: mocks.getQueryData })
}));

describe('optional product-tour activity', () => {
    beforeEach(() => vi.resetAllMocks());

    it('submits only the bounded activity contract without a browser telemetry key', async () => {
        // Arrange
        mocks.getQueryData.mockReturnValue({ email_address: 'private@example.test', product_tour_analytics_enabled: true });

        // Act
        await createProductTourActivity()('step-reached', 'app-overview', 1, 'catalog', 'navigation');

        // Assert
        expect(mocks.postJSON).toHaveBeenCalledExactlyOnceWith('users/me/product-tours/app-overview/activity', {
            action: 'step-reached',
            source: 'catalog',
            step: 'navigation',
            version: 1
        });
    });

    it.each([undefined, { product_tour_analytics_enabled: false }])('does not submit without an enabled preference: %j', async (user) => {
        // Arrange
        mocks.getQueryData.mockReturnValue(user);

        // Act
        await createProductTourActivity()('completed', 'app-overview', 1, 'catalog');

        // Assert
        expect(mocks.postJSON).not.toHaveBeenCalled();
    });

    it('checks the latest preference instead of capturing it at guide start', async () => {
        // Arrange
        const track = createProductTourActivity();
        mocks.getQueryData.mockReturnValue({ product_tour_analytics_enabled: true });
        await track('started', 'app-overview', 1, 'catalog');
        mocks.getQueryData.mockReturnValue({ product_tour_analytics_enabled: false });

        // Act
        await track('completed', 'app-overview', 1, 'catalog');

        // Assert
        expect(mocks.postJSON).toHaveBeenCalledTimes(1);
    });

    it('does not interrupt a guide when collection fails', async () => {
        // Arrange
        mocks.getQueryData.mockReturnValue({ product_tour_analytics_enabled: true });
        mocks.postJSON.mockRejectedValue(new Error('Unavailable'));

        // Act & Assert
        await expect(createProductTourActivity()('completed', 'app-overview', 1, 'catalog')).resolves.toBeUndefined();
    });
});
