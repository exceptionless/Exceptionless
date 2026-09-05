import { productTourCheckpoint } from '$features/product-tours/state.svelte';
import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import InvestigationListTour from './investigation-list.svelte';
vi.mock('$features/product-tours/api.svelte', () => ({ createProductTourActivity: () => vi.fn() }));

vi.mock('$features/product-tours/actions.svelte', () => ({
    createProductTourActions: () => ({ dismiss: vi.fn() })
}));

describe('InvestigationListTour', () => {
    let target: HTMLDivElement;

    beforeEach(() => {
        vi.stubGlobal(
            'ResizeObserver',
            class {
                disconnect() {}
                observe() {}
            }
        );
        target = document.createElement('div');
        target.dataset.tour = 'event-list';
        document.body.append(target);
        target.scrollIntoView = vi.fn();
        productTourCheckpoint.start('event-investigate', 'choose-error', 'catalog', 'user', 1);
    });

    afterEach(() => {
        cleanup();
        target.remove();
        vi.unstubAllGlobals();
        productTourCheckpoint.clear();
    });

    it('opens the supplied first error only after the user chooses the action', async () => {
        // Arrange
        const onOpenError = vi.fn();
        render(InvestigationListTour, { firstErrorId: 'first-error', onOpenError });
        const open = await screen.findByRole('button', { name: 'Open first error' });
        expect(onOpenError).not.toHaveBeenCalled();

        // Act
        await fireEvent.click(open);

        // Assert
        expect(onOpenError).toHaveBeenCalledExactlyOnceWith('first-error');
        expect(productTourCheckpoint.current?.checkpointName).toBe('choose-error');
    });

    it('offers no open action until an error is available', async () => {
        // Arrange
        const onOpenError = vi.fn();
        const component = render(InvestigationListTour, { onOpenError });
        await screen.findByText('No errors are ready to open in this list. Adjust the filters or wait for the results to load.');
        expect(screen.queryByRole('button', { name: 'Open first error' })).toBeNull();

        // Act
        await component.rerender({ firstErrorId: 'loaded-error', onOpenError });
        await fireEvent.click(await screen.findByRole('button', { name: 'Open first error' }));

        // Assert
        expect(onOpenError).toHaveBeenCalledExactlyOnceWith('loaded-error');
    });
});
