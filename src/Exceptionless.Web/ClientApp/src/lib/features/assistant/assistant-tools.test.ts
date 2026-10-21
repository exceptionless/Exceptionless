import { describe, expect, it } from 'vitest';

import { describeTimeRange, getAssistantToolDetail } from './assistant-tools';

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
});
