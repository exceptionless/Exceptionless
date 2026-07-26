import { render, screen } from '@testing-library/svelte';
import { describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    events: Array.from({ length: 20 }, (_, index) => ({ id: `event-${index}` }))
}));

vi.mock('$app/navigation', () => ({ goto: vi.fn() }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$app/state', () => ({ page: { params: { referenceId: 'reference-id' } } }));
vi.mock('$features/events/api.svelte', () => ({
    getEventsByReferenceQuery: () => ({
        data: { data: mocks.events, meta: { total: 25 } },
        error: undefined,
        isPending: false
    })
}));
vi.mock('$features/events/components/filters', () => ({
    ReferenceFilter: class {
        toFilter() {
            return 'reference:reference-id';
        }
    }
}));
vi.mock('$features/events/components/summary/summary.svelte', () => ({ default: () => undefined }));

import EventReferencePage, { formatReferenceResultCount } from './+page.svelte';

describe('event reference result count', () => {
    it('labels a truncated preview without claiming every result is displayed', () => {
        expect(formatReferenceResultCount(25, 20)).toBe('Showing 20 of 25 events for this reference.');
    });

    it('reports the exact count when every result is displayed', () => {
        expect(formatReferenceResultCount(2, 2)).toBe('Found 2 events for this reference.');
    });

    it('renders the server total and directs truncated results to the events list', () => {
        render(EventReferencePage);

        expect(screen.getByText('Showing 20 of 25 events for this reference.')).toBeTruthy();
        expect(screen.getByRole('link', { name: 'View In Events' })).toBeTruthy();
    });
});
