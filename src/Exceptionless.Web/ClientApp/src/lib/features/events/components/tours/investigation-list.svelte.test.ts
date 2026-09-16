import { productTourCheckpoint } from '$features/product-tours/state.svelte';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import InvestigationListTour from './investigation-list.svelte';
vi.mock('$features/product-tours/activity', () => ({ submitProductTourActivity: vi.fn() }));

vi.mock('$features/product-tours/actions.svelte', () => ({
    createProductTourActions: () => ({ dismiss: vi.fn() })
}));

describe('InvestigationListTour', () => {
    let target: HTMLDivElement;
    let filters: HTMLButtonElement;

    beforeEach(() => {
        vi.stubGlobal(
            'ResizeObserver',
            class {
                public disconnect() {}
                public observe() {}
            }
        );
        target = document.createElement('div');
        target.dataset.tour = 'event-list';
        target.innerHTML = '<table><tbody><tr tabindex="0"><td><a href="/next/event/first-error">Error</a></td></tr></tbody></table>';
        target.querySelector('tr')!.scrollIntoView = vi.fn();
        filters = document.createElement('button');
        filters.dataset.tour = 'event-filters';
        filters.scrollIntoView = vi.fn();
        document.body.append(target, filters);
        target.scrollIntoView = vi.fn();
        productTourCheckpoint.start('event-investigate', 'choose-error', 'user');
    });

    afterEach(() => {
        cleanup();
        target.remove();
        filters.remove();
        vi.unstubAllGlobals();
        productTourCheckpoint.clear();
    });

    it('opens the supplied first error only after the user chooses the action', async () => {
        // Arrange
        const onOpenError = vi.fn();
        render(InvestigationListTour, { firstErrorId: 'first-error', onOpenError });
        const open = await screen.findByRole('button', { name: 'Open error' });
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
        await screen.findByText('There are no errors in this list yet. Try a different time range or project.');
        expect(screen.queryByRole('button', { name: 'Open error' })).toBeNull();

        // Act
        target.querySelector('a')!.setAttribute('href', '/next/event/loaded-error');
        await component.rerender({ firstErrorId: 'loaded-error', onOpenError });
        await fireEvent.click(await screen.findByRole('button', { name: 'Open error' }));

        // Assert
        expect(onOpenError).toHaveBeenCalledExactlyOnceWith('loaded-error');
    });

    it('highlights the same error as its action when resuming on a mixed list', async () => {
        // Arrange: the event list has a newer log before the selected error.
        const errorId = '507f1f77bcf86cd799439012';
        target.innerHTML = `<table><tbody>
            <tr tabindex="0"><td><a href="/next/event/507f1f77bcf86cd799439011">Newer log</a></td></tr>
            <tr tabindex="0"><td><a href="/next/event/${errorId}">Checkout error</a></td></tr>
        </tbody></table>`;
        const errorRow = target.querySelectorAll('tr')[1];
        const onOpenError = vi.fn();
        productTourCheckpoint.start('event-investigate', 'choose-error', 'user');

        // Act: mount the real guide on the existing list.
        render(InvestigationListTour, { firstErrorId: errorId, onOpenError });

        // Assert
        await waitFor(() => expect(document.querySelector('.driver-active-element')).toBe(errorRow));
        await fireEvent.click(document.querySelector('.driver-popover-next-btn')!);
        expect(onOpenError).toHaveBeenCalledWith(errorId);
    });
});
