import { describe, expect, it } from 'vitest';

import { describeTimeRange, getAssistantToolDetail, getAssistantToolLabel, getAssistantToolSavedView, isMutationTool } from './assistant-tools';

describe('describeTimeRange', () => {
    it.each([
        ['[now-24h TO now]', 'last 24 hours'],
        ['[now-7d TO now]', 'last 7 days'],
        ['[now/d TO now/d]', 'today'],
        ['[now-1d/d  TO now-1d/d]', 'yesterday'],
        ['[now/w TO now/w]', 'this week'],
        ['[2026-06-01 TO 2026-06-30]', '[2026-06-01 TO 2026-06-30]']
    ])('names %s as %s', (time, expected) => {
        expect(describeTimeRange(time)).toBe(expected);
    });

    it('shows the time range a search used', () => {
        const detail = getAssistantToolDetail(
            {
                arguments: JSON.stringify({ filter: 'type:error', time: '[now-24h TO now]' }),
                id: 'tool-1',
                name: 'search_stacks',
                status: 'complete'
            },
            new Map()
        );

        expect(detail).toBe('type:error · last 24 hours');
    });

    it('shows how counted events were grouped', () => {
        const detail = getAssistantToolDetail(
            {
                arguments: JSON.stringify({ filter: 'type:error', groupBy: 'stack', time: '[now/d TO now/d]' }),
                id: 'tool-1',
                name: 'count_events',
                status: 'complete'
            },
            new Map()
        );

        expect(detail).toBe('type:error · today · by stack');
    });
});

describe('saved view tools', () => {
    const view = { filter: 'type:error', id: 'view-1', name: 'Production errors', slug: 'production-errors', view_type: 'stacks' };

    it.each([
        ['create_saved_view', 'Created a saved view'],
        ['update_saved_view', 'Updated a saved view'],
        ['delete_saved_view', 'Deleted a saved view']
    ])('reports %s as a named change', (name, label) => {
        // Arrange
        const tool = {
            arguments: '{}',
            id: 'tool-1',
            name,
            result: JSON.stringify({ data: { ...view, changed: true }, ok: true }),
            status: 'complete' as const
        };

        // Act
        const detail = getAssistantToolDetail(tool, new Map());

        // Assert
        expect(isMutationTool(tool)).toBe(true);
        expect(getAssistantToolLabel(tool, false)).toBe(label);
        expect(detail).toBe('Production errors');
    });

    it('shows when an update changed nothing', () => {
        // Arrange
        const tool = {
            arguments: '{}',
            id: 'tool-1',
            name: 'update_saved_view',
            result: JSON.stringify({ data: { ...view, changed: false }, ok: true }),
            status: 'complete' as const
        };

        // Act
        const detail = getAssistantToolDetail(tool, new Map());

        // Assert
        expect(detail).toBe('No change needed · Production errors');
    });

    it('links to a created view but not a deleted one', () => {
        // Arrange
        const result = JSON.stringify({ data: view, ok: true });
        const created = { arguments: '{}', id: 'tool-1', name: 'create_saved_view', result, status: 'complete' as const };
        const deleted = { arguments: '{}', id: 'tool-2', name: 'delete_saved_view', result, status: 'complete' as const };

        // Act
        const createdView = getAssistantToolSavedView(created);
        const deletedView = getAssistantToolSavedView(deleted);

        // Assert
        expect(createdView).toEqual({ id: 'view-1', name: 'Production errors', slug: 'production-errors', view_type: 'stacks' });
        expect(deletedView).toBeUndefined();
    });
});
