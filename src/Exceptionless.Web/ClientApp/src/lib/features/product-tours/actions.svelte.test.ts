import { afterEach, describe, expect, it, vi } from 'vitest';

import { createProductTourActions } from './actions.svelte';
import { productTourCheckpoint } from './state.svelte';

const mocks = vi.hoisted(() => ({
    error: vi.fn(),
    mutateAsync: vi.fn<() => Promise<void>>(),
    openCatalog: vi.fn(),
    submitFeatureUsage: vi.fn(),
    success: vi.fn()
}));
vi.mock('./api.svelte', () => ({ createProductTourActivity: () => mocks.submitFeatureUsage }));
vi.mock('$features/users/api.svelte', () => ({ putCurrentUserProductTour: () => ({ mutateAsync: mocks.mutateAsync }) }));
vi.mock('./controls.svelte', () => ({ tryUseProductTourControls: () => ({ openCatalog: mocks.openCatalog }) }));
vi.mock('svelte-sonner', () => ({ toast: { error: mocks.error, success: mocks.success } }));

describe('product tour completion', () => {
    afterEach(() => {
        productTourCheckpoint.clear();
        vi.resetAllMocks();
    });

    it('offers an actionable next step only after progress is saved', async () => {
        // Arrange
        const checkpoint = productTourCheckpoint.start('event-investigate', 'filter-stack-events', 'catalog', 'user', 1);
        const actions = createProductTourActions();

        // Act
        const completed = await actions.complete(checkpoint);

        // Assert
        expect(completed).toBe(true);
        expect(productTourCheckpoint.current).toBeUndefined();
        expect(mocks.success).toHaveBeenCalledExactlyOnceWith('You’ve explored an error and its occurrences', {
            action: { label: 'Browse guides', onClick: mocks.openCatalog },
            description: 'For more guides, select your name in the sidebar → Help → Guided Tours.'
        });
        const options = mocks.success.mock.calls[0]![1];
        options.action.onClick();
        expect(mocks.openCatalog).toHaveBeenCalledOnce();
    });

    it('leaves the overview menu handoff unobstructed by a completion toast', async () => {
        // Arrange
        const checkpoint = productTourCheckpoint.start('app-overview', 'help', 'catalog', 'user', 1);

        // Act
        const completed = await createProductTourActions().complete(checkpoint);

        // Assert
        expect(completed).toBe(true);
        expect(mocks.success).not.toHaveBeenCalled();
    });

    it('keeps the last step available when progress cannot be saved', async () => {
        // Arrange
        const checkpoint = productTourCheckpoint.start('app-overview', 'help', 'catalog', 'user', 1);
        mocks.mutateAsync.mockRejectedValueOnce(new Error('Unavailable'));

        // Act
        const completed = await createProductTourActions().complete(checkpoint);

        // Assert
        expect(completed).toBe(false);
        expect(productTourCheckpoint.current).toBe(checkpoint);
        expect(mocks.error).toHaveBeenCalledOnce();
        expect(mocks.success).not.toHaveBeenCalled();
        expect(mocks.openCatalog).not.toHaveBeenCalled();
    });

    it('does not show a completion action for dismissal or a stale checkpoint', async () => {
        // Arrange
        const checkpoint = productTourCheckpoint.start('app-overview', 'help', 'catalog', 'user', 1);
        const actions = createProductTourActions();

        // Act
        await actions.dismiss(checkpoint);
        const completed = await actions.complete(checkpoint);

        // Assert
        expect(completed).toBe(false);
        expect(mocks.success).not.toHaveBeenCalled();
        expect(mocks.openCatalog).not.toHaveBeenCalled();
    });

    it('offers the next guide once after a first event succeeds', async () => {
        // Arrange
        const checkpoint = productTourCheckpoint.start('project-configure', 'event-received', 'catalog', 'user', 1);
        mocks.mutateAsync.mockResolvedValueOnce(undefined);
        const actions = createProductTourActions();

        // Act
        actions.completeAfterDomainSuccess(checkpoint);
        actions.completeAfterDomainSuccess(checkpoint);
        await vi.waitFor(() => expect(productTourCheckpoint.current).toBeUndefined());

        // Assert
        expect(mocks.mutateAsync).toHaveBeenCalledOnce();
        expect(mocks.success).toHaveBeenCalledExactlyOnceWith(
            'Your project received its first event',
            expect.objectContaining({
                action: { label: 'Browse guides', onClick: mocks.openCatalog }
            })
        );
    });

    it('preserves a domain-success checkpoint for retry when progress saving fails', async () => {
        // Arrange
        const checkpoint = productTourCheckpoint.start('project-configure', 'event-received', 'catalog', 'user', 1);
        mocks.mutateAsync.mockRejectedValueOnce(new Error('Unavailable'));

        // Act
        createProductTourActions().completeAfterDomainSuccess(checkpoint);
        await vi.waitFor(() => expect(mocks.error).toHaveBeenCalledOnce());

        // Assert
        expect(productTourCheckpoint.current).toBe(checkpoint);
        expect(mocks.success).not.toHaveBeenCalled();
    });
});
