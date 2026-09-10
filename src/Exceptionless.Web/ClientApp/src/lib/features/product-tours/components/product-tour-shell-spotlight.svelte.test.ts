import { cleanup, render, waitFor } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { productTourCheckpoint } from '../state.svelte';
import ProductTourShellSpotlight from './product-tour-shell-spotlight.svelte';

vi.mock('../actions.svelte', () => ({ createProductTourActions: () => ({ complete: vi.fn(), dismiss: vi.fn() }) }));
vi.mock('../activity', () => ({ submitProductTourActivity: vi.fn() }));

afterEach(() => {
    cleanup();
    productTourCheckpoint.clear();
});

describe('ProductTourShellSpotlight', () => {
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
