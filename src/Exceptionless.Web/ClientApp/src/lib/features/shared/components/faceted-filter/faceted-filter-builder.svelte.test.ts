import { DateFilter, KeywordFilter } from '$features/events/components/filters/models.svelte';
import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

import type { IFilter } from './models';

import Harness from './faceted-filter-builder.test-harness.svelte';

describe('faceted filter initialization', () => {
    it('keeps duplicate raw filters distinct when another filter is added', async () => {
        // Arrange
        const local = new KeywordFilter('error.type:Local');
        const remote = new KeywordFilter('error.type:Remote');
        const view = render(Harness, { changed: vi.fn(), filters: [local], remove: vi.fn() });
        const original = await screen.findByRole('button', { name: /^Raw Filter.*error\.type:Local/ });

        // Act
        await view.rerender({ filters: [local, remote] });

        // Assert
        expect(screen.getByRole('button', { name: /^Raw Filter.*error\.type:Local/ })).toBe(original);
        expect(screen.getByRole('button', { name: /^Raw Filter.*error\.type:Remote/ })).not.toBe(original);

        await view.rerender({ filters: [new KeywordFilter('error.type:Local'), new KeywordFilter('error.type:Remote')] });
        expect(screen.getAllByRole('button', { name: /^Raw Filter/ })).toHaveLength(2);
    });

    it('opens a newly added filter after the parent supplies it', async () => {
        // Arrange
        const changed = vi.fn<(filter: IFilter) => void>();
        const view = render(Harness, { changed, filters: [], remove: vi.fn() });
        await fireEvent.click(screen.getByRole('button', { name: 'Manage filters' }));

        // Act
        await fireEvent.click(await screen.findByRole('option', { name: 'Date' }));
        expect(changed).toHaveBeenCalledOnce();
        const added = changed.mock.calls[0]![0];
        await view.rerender({ filters: [added] });

        // Assert
        expect(screen.getByRole('button', { name: /^Date/ }).getAttribute('aria-expanded')).toBe('true');
    });

    it('does not reopen a removed filter when it is added again', async () => {
        // Arrange
        const view = render(Harness, { changed: vi.fn(), filters: [new DateFilter('date', '[now-90d TO now]')], remove: vi.fn() });
        await fireEvent.click(await screen.findByRole('button', { name: /^Date/ }));
        await screen.findByRole('button', { name: 'Last 30 days' });

        // Act
        await view.rerender({ filters: [] });
        await view.rerender({ filters: [new DateFilter('date', '[now-7d TO now]')] });

        // Assert
        expect(screen.getByRole('button', { name: /^Date/ }).getAttribute('aria-expanded')).toBe('false');
    });

    it('keeps the date picker open when hydration replaces a filter instance', async () => {
        // Arrange
        const changed = vi.fn();
        const initial = new DateFilter('date', '[now-90d TO now]');
        const hydrated = new DateFilter('date', '[now-90d TO now]');
        const view = render(Harness, { changed, filters: [initial], remove: vi.fn() });
        const trigger = await screen.findByRole('button', { name: /^Date/ });
        await fireEvent.click(trigger);
        await screen.findByRole('button', { name: 'Last 30 days' });

        // Act
        await view.rerender({ filters: [hydrated] });

        // Assert
        await waitFor(() => expect(trigger.getAttribute('aria-expanded')).toBe('true'));
        expect(screen.getByRole('button', { name: /^Date/ })).toBe(trigger);
        await fireEvent.click(screen.getByRole('button', { name: 'Last 30 days' }));
        expect(changed).toHaveBeenCalledWith(hydrated);
        expect(hydrated.value).toBe('[now-30d TO now]');
    });
});
