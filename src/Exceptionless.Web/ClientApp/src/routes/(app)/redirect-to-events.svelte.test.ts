import { DateFilter, ProjectFilter, StatusFilter, StringFilter } from '$features/events/components/filters/models.svelte';
import { describe, expect, it, vi } from 'vitest';

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
        expect(url.searchParams.get('stack')).toBe('stack-1');
        expect(url.searchParams.has('filters')).toBe(false);
    });

    it('maps project filters to the project query parameter', async () => {
        // Arrange
        const { buildListPageHref } = await import('./redirect-to-events.svelte');

        // Act
        const href = buildListPageHref('events', 'org-1', [new ProjectFilter(['project-1'])]);
        const url = new URL(href, 'https://example.test');

        // Assert
        expect(url.pathname).toBe('/(app)/event');
        expect(url.searchParams.get('project')).toBe('project-1');
        expect(url.searchParams.has('filters')).toBe(false);
    });

    it('maps registered filters to explicit query parameters', async () => {
        // Arrange
        const { buildListPageHref } = await import('./redirect-to-events.svelte');

        // Act
        const href = buildListPageHref('events', 'org-1', [new StatusFilter(['open', 'regressed'] as never[])]);
        const url = new URL(href, 'https://example.test');

        // Assert
        expect(url.pathname).toBe('/(app)/event');
        expect(url.searchParams.get('status')).toBe('open,regressed');
        expect(url.searchParams.has('filter')).toBe(false);
        expect(url.searchParams.has('filters')).toBe(false);
    });

    it('falls back to raw filter expressions for unmapped filters', async () => {
        // Arrange
        const { buildListPageHref } = await import('./redirect-to-events.svelte');

        // Act
        const href = buildListPageHref('events', 'org-1', [new StringFilter('message', 'hello')]);
        const url = new URL(href, 'https://example.test');

        // Assert
        expect(url.pathname).toBe('/(app)/event');
        expect(url.searchParams.get('filter')).toBe('message:hello');
        expect(url.searchParams.has('filters')).toBe(false);
    });
});
