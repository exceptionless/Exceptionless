import { cleanup, fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, describe, expect, it, vi } from 'vitest';

import EventsSelectAllBanner from './events-select-all-banner.svelte';

function bannerText(): string {
    return (screen.getByRole('status').textContent ?? '').replace(/\s+/g, ' ').trim();
}

describe('EventsSelectAllBanner', () => {
    afterEach(cleanup);

    it('offers to select everything matching the filter once the page is selected', async () => {
        // Arrange
        const onSelectAllMatching = vi.fn();
        render(EventsSelectAllBanner, { props: { isPageSelected: true, onClear: vi.fn(), onSelectAllMatching, pageCount: 20, totalMatching: 1234 } });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Select all 1,234 events matching this filter' }));

        // Assert
        expect(bannerText()).toBe('All 20 events on this page are selected. Select all 1,234 events matching this filter');
        expect(onSelectAllMatching).toHaveBeenCalledOnce();
    });

    it('confirms that everything is selected and offers to clear it', async () => {
        // Arrange
        const onClear = vi.fn();
        render(EventsSelectAllBanner, {
            props: { allMatchingCount: 1234, isPageSelected: true, onClear, onSelectAllMatching: vi.fn(), pageCount: 20, totalMatching: 1234 }
        });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Clear selection' }));

        // Assert
        expect(bannerText()).toBe('All 1,234 events are selected · Clear selection');
        expect(screen.queryByRole('button', { name: /Select all/ })).toBeNull();
        expect(onClear).toHaveBeenCalledOnce();
    });

    it.each([
        ['only some rows are selected', { isPageSelected: false, pageCount: 20, totalMatching: 1234 }],
        ['the page already shows every match', { isPageSelected: true, pageCount: 20, totalMatching: 20 }],
        ['the total is not known yet', { isPageSelected: true, pageCount: 20, totalMatching: undefined }]
    ])('shows nothing when %s', (_, props) => {
        // Arrange & Act
        render(EventsSelectAllBanner, { props: { ...props, onClear: vi.fn(), onSelectAllMatching: vi.fn() } });

        // Assert
        expect(screen.queryByRole('status')).toBeNull();
    });
});
