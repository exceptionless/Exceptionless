import { cleanup, render, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { productTourCheckpoint } from '../state.svelte';
import ProductTourShellSpotlight from './product-tour-shell-spotlight.svelte';

vi.mock('../actions.svelte', () => ({ createProductTourActions: () => ({ complete: vi.fn(), dismiss: vi.fn() }) }));
vi.mock('../activity', () => ({ submitProductTourActivity: vi.fn() }));

let targets: HTMLDivElement;

beforeEach(() => {
    vi.stubGlobal(
        'ResizeObserver',
        class {
            public disconnect() {}
            public observe() {}
        }
    );
    targets = document.createElement('div');
    for (const name of ['navigation-stacks', 'navigation-events', 'event-filters']) {
        const target = document.createElement('button');
        target.dataset.tour = name;
        target.scrollIntoView = vi.fn();
        targets.append(target);
    }
    document.body.append(targets);
});

afterEach(() => {
    cleanup();
    productTourCheckpoint.clear();
    targets.remove();
    vi.unstubAllGlobals();
});

describe('ProductTourShellSpotlight', () => {
    it.each(['navigation', 'events'] as const)('opens navigation when %s switches to mobile', async (checkpointName) => {
        productTourCheckpoint.start('app-overview', checkpointName, 'user');
        const checkpoint = productTourCheckpoint.current!;
        const setMobileNavigationOpen = vi.fn();
        const { rerender } = render(ProductTourShellSpotlight, {
            checkpoint,
            isAnyOverlayOpen: false,
            isMobile: false,
            openAssistant: vi.fn(),
            setMobileNavigationOpen
        });
        await waitFor(() => expect(setMobileNavigationOpen).toHaveBeenCalledWith(true));
        setMobileNavigationOpen.mockClear();

        await rerender({ isMobile: true });

        await waitFor(() => expect(setMobileNavigationOpen).toHaveBeenCalledWith(true));
        expect(productTourCheckpoint.current).toEqual(checkpoint);
        setMobileNavigationOpen.mockClear();

        await rerender({ isMobile: false });

        await waitFor(() => expect(setMobileNavigationOpen).toHaveBeenCalledWith(true));
        expect(productTourCheckpoint.current).toEqual(checkpoint);
    });

    it('keeps navigation closed when a non-navigation step switches to mobile', async () => {
        productTourCheckpoint.start('app-overview', 'filters', 'user');
        const setMobileNavigationOpen = vi.fn();
        const { rerender } = render(ProductTourShellSpotlight, {
            checkpoint: productTourCheckpoint.current!,
            isAnyOverlayOpen: false,
            isMobile: false,
            openAssistant: vi.fn(),
            setMobileNavigationOpen
        });

        await rerender({ isMobile: true });

        await waitFor(() => expect(setMobileNavigationOpen).toHaveBeenCalledWith(false));
        expect(setMobileNavigationOpen).not.toHaveBeenCalledWith(true);
    });

    it.each([undefined, { enabled: true, has_access: false, upgrade_required: true }])(
        'resumes an unavailable Exie checkpoint at Search: %j',
        async (assistantAccess) => {
            // Arrange
            productTourCheckpoint.start('app-overview', 'exie', 'user');

            // Act
            render(ProductTourShellSpotlight, {
                assistantAccess,
                checkpoint: productTourCheckpoint.current!,
                isAnyOverlayOpen: false,
                isMobile: false,
                openAssistant: vi.fn(),
                setMobileNavigationOpen: vi.fn()
            });

            // Assert
            await waitFor(() => expect(productTourCheckpoint.current?.checkpointName).toBe('command-search'));
            expect(productTourCheckpoint.current?.tourName).toBe('app-overview');
        }
    );
});
