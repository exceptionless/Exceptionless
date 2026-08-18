import type { EventSummaryModel } from '$features/events/components/summary';

import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import SessionDurationCell from './session-duration-cell.svelte';

describe('SessionDurationCell', () => {
    it('shows only the two largest time units', () => {
        const summary: EventSummaryModel<'event-session-summary'> = {
            data: {
                SessionEnd: '2026-08-17T02:00:00Z',
                Value: '5537'
            },
            date: '2026-08-17T00:00:00Z',
            id: 'event-id',
            project_id: 'project-id',
            tags: [],
            template_key: 'event-session-summary'
        };

        render(SessionDurationCell, { summary });

        expect(screen.getByText('1 hour 32 minutes')).toBeTruthy();
        expect(screen.queryByText(/second/)).toBeNull();
    });
});
