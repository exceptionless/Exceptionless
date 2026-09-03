import { ProductTourStatus } from '$features/users/models';
import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { getProductTourItems } from '../../catalog';
import ProductTourCatalogDialog from './product-tour-catalog-dialog.svelte';

describe('ProductTourCatalogDialog', () => {
    beforeEach(() => vi.useFakeTimers());
    afterEach(async () => {
        cleanup();
        await vi.runOnlyPendingTimersAsync();
        vi.useRealTimers();
    });
    it('distinguishes guides and preserves restart, continue, and unavailable actions', async () => {
        // Arrange
        const items = getProductTourItems(
            {
                errorEventAvailability: 'empty',
                isProjectConfigurePage: false,
                isSetupPage: false,
                organizationId: 'organization',
                pathname: '/next',
                projects: []
            },
            { 'app-overview': { status: ProductTourStatus.Completed, version: 1 } }
        );
        const onStart = vi.fn(async () => {});
        render(ProductTourCatalogDialog, { items, onStart, open: true, ready: true, resumableTourName: 'saved-view-create' });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Restart Explore Exceptionless' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Continue Create a saved view' }));
        screen.getByRole('button', { name: 'Start Investigate an error' }).click();

        // Assert
        expect(onStart.mock.calls).toEqual([['app-overview'], ['saved-view-create']]);
        expect(screen.getByText('Completed')).toBeTruthy();
        const unavailable = screen.getByRole('button', { name: 'Start Investigate an error' });
        expect(unavailable.hasAttribute('disabled')).toBe(true);
        expect(document.getElementById(unavailable.getAttribute('aria-describedby')!)?.textContent).toContain('Send an error report');
        expect(screen.getByRole('list', { name: 'Available guides' })).toBeTruthy();
        expect(screen.getAllByRole('listitem')).toHaveLength(5);
        const icons = items.map((item) => {
            const icon = screen.getByRole('region', { name: item.title }).querySelector('svg');
            expect(icon?.getAttribute('aria-hidden')).toBe('true');
            return icon?.innerHTML;
        });
        expect(new Set(icons).size).toBe(5);
    });

    it('keeps the picker focused on outcomes without step counts or documentation detours', () => {
        // Arrange
        const items = getProductTourItems({
            errorEventAvailability: 'empty',
            isProjectConfigurePage: false,
            isSetupPage: false,
            pathname: '/next',
            projects: []
        });
        const onStart = vi.fn();

        // Act
        render(ProductTourCatalogDialog, { items, onStart, open: true, ready: false });

        // Assert
        for (const item of items) {
            expect(screen.getByText(item.description)).toBeTruthy();
        }
        expect(screen.getByRole('link', { name: 'Usage settings' }).getAttribute('href')).toContain('/account/manage#guided-tour-privacy');
        expect(screen.queryAllByRole('link')).toHaveLength(1);
        expect(screen.queryByText(/\d+ steps/)).toBeNull();
        expect(onStart).not.toHaveBeenCalled();
    });
});
