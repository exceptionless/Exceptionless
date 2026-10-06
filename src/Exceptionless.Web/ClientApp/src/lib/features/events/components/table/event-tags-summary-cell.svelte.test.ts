import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import EventTagsSummaryCell from './event-tags-summary-cell.svelte';

describe('EventTagsSummaryCell', () => {
    it('keeps compact mode to two tags and reveals more when wrapping', () => {
        render(EventTagsSummaryCell, { tags: ['api', 'production', 'critical', 'customer'] });

        expect(screen.getByText('api')).toBeTruthy();
        expect(screen.getByText('production')).toBeTruthy();
        expect(screen.getByText('+2').closest<HTMLElement>('[data-slot="tooltip-trigger"]')?.classList).toContain('group-data-[wrap=true]/wrapped:hidden');

        const thirdTagTrigger = screen.getByText('critical').closest<HTMLElement>('[data-slot="tooltip-trigger"]');
        expect(thirdTagTrigger?.classList).toContain('hidden');
        expect(thirdTagTrigger?.classList).toContain('group-data-[wrap=true]/wrapped:inline-flex');
        expect(screen.getByLabelText('Tags: api, production, critical, customer').getAttribute('title')).toBeNull();
    });

    it('shows an empty value when there are no tags', () => {
        render(EventTagsSummaryCell, { tags: [] });

        expect(screen.getByText('—')).toBeTruthy();
    });
});
