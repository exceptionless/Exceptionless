import { DateFilter, ProjectFilter, StatusFilter, StringFilter } from '$features/events/components/filters/models.svelte';
import { describe, expect, it, vi } from 'vitest';

vi.mock('$app/navigation', () => ({
    goto: vi.fn()
}));

vi.mock('$app/paths', () => ({
    resolve: (path: string) => path
}));

describe('redirect-to-events', () => {
    it('clears every list filter query parameter without changing unrelated state', async () => {
        // Arrange
        const { clearListFilterQueryParams } = await import('./redirect-to-events.svelte');
        const queryParams = {
            bot: 'true',
            filter: 'message:test',
            first: 'false',
            level: 'error',
            limit: 20,
            project: 'project-1',
            reference: 'reference-1',
            session: 'session-1',
            stack: 'stack-1',
            status: 'open',
            tag: 'important',
            time: '1h',
            type: 'error',
            version: '1.0.0'
        };

        // Act
        clearListFilterQueryParams(queryParams);

        // Assert
        expect(queryParams).toEqual({
            bot: null,
            filter: null,
            first: null,
            level: null,
            limit: 20,
            project: null,
            reference: null,
            session: null,
            stack: null,
            status: null,
            tag: null,
            time: null,
            type: null,
            version: null
        });
    });

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

    it('uses relative duration shortcuts for time query parameters', async () => {
        // Arrange
        const { buildListPageHref, deserializeTimeQueryParam } = await import('./redirect-to-events.svelte');

        // Act
        const href = buildListPageHref('events', 'org-1', [new DateFilter('date', '[now-1h TO now]')]);
        const url = new URL(href, 'https://example.test');

        // Assert
        expect(url.searchParams.get('time')).toBe('1h');
        expect(deserializeTimeQueryParam(url.searchParams.get('time')!)).toBe('[now-1h TO now]');
    });

    it('omits date range brackets from custom time query parameters', async () => {
        // Arrange
        const { buildListPageHref, deserializeTimeQueryParam } = await import('./redirect-to-events.svelte');

        // Act
        const href = buildListPageHref('events', 'org-1', [new DateFilter('date', '[2025-01-01 TO 2025-02-01]')]);
        const url = new URL(href, 'https://example.test');

        // Assert
        expect(url.searchParams.get('time')).toBe('2025-01-01 TO 2025-02-01');
        expect(deserializeTimeQueryParam(url.searchParams.get('time')!)).toBe('[2025-01-01 TO 2025-02-01]');
    });

    it('accepts existing expanded time query parameters', async () => {
        // Arrange
        const { deserializeTimeQueryParam } = await import('./redirect-to-events.svelte');

        // Act
        const time = deserializeTimeQueryParam('now-1h TO now');

        // Assert
        expect(time).toBe('[now-1h TO now]');
    });

    it('accepts existing bracketed time query parameters', async () => {
        // Arrange
        const { deserializeTimeQueryParam } = await import('./redirect-to-events.svelte');

        // Act
        const time = deserializeTimeQueryParam('[now-1h TO now]');

        // Assert
        expect(time).toBe('[now-1h TO now]');
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
