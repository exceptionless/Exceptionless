import { describe, expect, it, vi } from 'vitest';

import { DateFilter, StringFilter } from '$features/events/components/filters/models.svelte';

vi.mock('$app/navigation', () => ({
    goto: vi.fn()
}));

vi.mock('$app/paths', () => ({
    resolve: (path: string) => path
}));

describe('redirect-to-events', () => {
    it('uses an explicit all-time query value for stack event drilldowns', async () => {
        // Arrange
        const { ALL_TIME_QUERY_VALUE, buildListPageHref, getEventsNavigationOptionsForFilter } = await import('./redirect-to-events.svelte');
        const stackFilter = new StringFilter('stack', 'stack-1');
        const options = getEventsNavigationOptionsForFilter(stackFilter);

        // Act
        const href = buildListPageHref('events', 'org-1', [new DateFilter('date', '[now-7d TO now]'), stackFilter], options);
        const url = new URL(href, 'https://example.test');

        // Assert
        expect(url.pathname).toBe('/(app)/event');
        expect(url.searchParams.get('time')).toBe(ALL_TIME_QUERY_VALUE);
        expect(url.searchParams.get('filters')).toBe('[{"type":"string","term":"stack","value":"stack-1"}]');
    });
});
