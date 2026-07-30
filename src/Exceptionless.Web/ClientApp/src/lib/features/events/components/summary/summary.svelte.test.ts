import { StackStatus } from '$features/stacks/models';
import { render, screen } from '@testing-library/svelte';
import { describe, expect, it } from 'vitest';

import type { EventSummaryModel, StackSummaryModel } from './index';

import Summary from './summary.svelte';

describe('Summary', () => {
    it('links an event summary to that event details page', () => {
        const summary: EventSummaryModel<'event-error-summary'> = {
            data: {
                Message: 'Unexpected end of Stream, the content may have already been read by another component.',
                Method: 'MoveNext',
                Type: 'IOException'
            },
            date: '2026-07-28T00:00:00Z',
            id: 'event-id',
            project_id: 'project-id',
            tags: [],
            template_key: 'event-error-summary'
        };

        render(Summary, { showStatus: false, summary });

        expect(screen.getByRole('link', { name: summary.data.Message }).getAttribute('href')).toBe('/next/event/event-id');
    });

    it('renders an event summary without an internal link when linking is disabled', () => {
        const summary: EventSummaryModel<'event-error-summary'> = {
            data: {
                Message: 'Unexpected end of Stream, the content may have already been read by another component.',
                Method: 'MoveNext',
                Type: 'IOException'
            },
            date: '2026-07-28T00:00:00Z',
            id: 'event-id',
            project_id: 'project-id',
            tags: [],
            template_key: 'event-error-summary'
        };

        const { container } = render(Summary, { linkToDetails: false, showStatus: false, summary });

        expect(screen.queryByRole('link')).toBeNull();
        expect(container.textContent).toContain(summary.data.Message!);
    });

    it('links a stack summary to stack details', () => {
        const summary: StackSummaryModel<'stack-error-summary'> = {
            data: {
                Message: 'Unexpected end of Stream, the content may have already been read by another component.',
                Method: 'MoveNext',
                Type: 'IOException'
            },
            first_occurrence: '2026-07-28T00:00:00Z',
            id: 'stack-id',
            last_occurrence: '2026-07-28T00:00:00Z',
            project_id: 'project-id',
            status: StackStatus.Open,
            tags: [],
            template_key: 'stack-error-summary',
            title: 'Unexpected end of Stream, the content may have already been read by another component.',
            total: 1,
            total_users: 1,
            users: 1
        };

        render(Summary, { showStatus: false, summary });

        expect(screen.getByRole('link', { name: summary.title }).getAttribute('href')).toBe('/next/stack/stack-id');
    });
});
