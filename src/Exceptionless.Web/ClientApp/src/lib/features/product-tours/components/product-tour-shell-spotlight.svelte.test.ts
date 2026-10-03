import { page } from '$app/state';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { productTourCheckpoint } from '../state.svelte';
import ProductTourShellSpotlight from './product-tour-shell-spotlight.svelte';

vi.mock('../actions.svelte', () => ({ createProductTourActions: () => ({ complete: vi.fn(), dismiss: vi.fn() }) }));
vi.mock('../activity', () => ({ submitProductTourActivity: vi.fn() }));

const navigation = vi.hoisted(() => ({ goto: vi.fn() }));
vi.mock('$app/navigation', () => navigation);
vi.mock('$app/state', () => ({ page: { route: { id: '/(app)/stack' } } }));
vi.mock('$app/paths', () => ({ resolve: () => '/next/event' }));

let targets: HTMLDivElement;

beforeEach(() => {
    navigation.goto.mockReset();
    page.route.id = '/(app)/stack';
    vi.stubGlobal(
        'ResizeObserver',
        class {
            public disconnect() {}
            public observe() {}
        }
    );
    targets = document.createElement('div');
    for (const name of ['navigation-stacks', 'navigation-events', 'event-filters', 'saved-view-trigger']) {
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
    it.each([
        ['events', 'Next'],
        ['saved-views', 'Back']
    ] as const)('returns to Events before moving from %s to filters', async (step, button) => {
        productTourCheckpoint.start('app-overview', step, 'user');
        const checkpoint = productTourCheckpoint.current!;
        const pending = Promise.withResolvers<void>();
        navigation.goto.mockReturnValueOnce(pending.promise);
        render(ProductTourShellSpotlight, {
            checkpoint,
            isAnyOverlayOpen: false,
            isMobile: false,
            openAssistant: vi.fn(),
            setMobileNavigationOpen: vi.fn()
        });

        await fireEvent.click(await screen.findByRole('button', { name: button }));

        expect(navigation.goto).toHaveBeenCalledWith('/next/event');
        expect(productTourCheckpoint.current).toBe(checkpoint);
        pending.resolve();
        await waitFor(() => expect(productTourCheckpoint.current?.checkpointName).toBe('filters'));
    });

    it.each(['/(app)/event', '/(app)/event/[slug=savedview]'] as const)('preserves the current event view on %s', async (routeId) => {
        page.route.id = routeId;
        productTourCheckpoint.start('app-overview', 'events', 'user');
        render(ProductTourShellSpotlight, {
            checkpoint: productTourCheckpoint.current!,
            isAnyOverlayOpen: false,
            isMobile: false,
            openAssistant: vi.fn(),
            setMobileNavigationOpen: vi.fn()
        });

        await fireEvent.click(await screen.findByRole('button', { name: 'Next' }));

        expect(navigation.goto).not.toHaveBeenCalled();
        expect(productTourCheckpoint.current?.checkpointName).toBe('filters');
    });

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

    it.each([
        ['navigation', 'navigation-stacks'],
        ['events', 'navigation-events']
    ] as const)('reopens mobile navigation after following the %s target', async (checkpointName, targetName) => {
        productTourCheckpoint.start('app-overview', checkpointName, 'user');
        const checkpoint = productTourCheckpoint.current!;
        const setMobileNavigationOpen = vi.fn();
        render(ProductTourShellSpotlight, {
            checkpoint,
            isAnyOverlayOpen: false,
            isMobile: true,
            openAssistant: vi.fn(),
            setMobileNavigationOpen
        });
        await waitFor(() => expect(setMobileNavigationOpen).toHaveBeenCalledWith(true));
        setMobileNavigationOpen.mockClear();

        const target = targets.querySelector(`[data-tour="${targetName}"]`)!;
        target.addEventListener('click', () => setMobileNavigationOpen(false));
        await fireEvent.click(target);

        await waitFor(() => expect(setMobileNavigationOpen).toHaveBeenLastCalledWith(true));
        expect(productTourCheckpoint.current).toBe(checkpoint);
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
