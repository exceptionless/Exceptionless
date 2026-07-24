import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import EventTagsSummaryCell from './event-tags-summary-cell.svelte';

describe('EventTagsSummaryCell', () => {
    it('shows two tags and summarizes the remaining tags', () => {
        render(EventTagsSummaryCell, { tags: ['api', 'production', 'critical', 'customer'] });

        expect(screen.getByText('api')).toBeTruthy();
        expect(screen.getByText('production')).toBeTruthy();
        expect(screen.getByText('+2')).toBeTruthy();
        expect(screen.queryByText('critical')).toBeNull();
        expect(screen.getByLabelText('Tags: api, production, critical, customer').getAttribute('title')).toBe('api, production, critical, customer');
    });

    it('shows an empty value when there are no tags', () => {
        render(EventTagsSummaryCell, { tags: [] });

        expect(screen.getByText('—')).toBeTruthy();
    });
});
