import { fireEvent, render, screen, within } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { shallowRouting } from '../shallow-routing.test-fake.svelte';
import QueryParametersTestHarness from './query-params.test-harness.svelte';

vi.mock('$app/env', () => ({ browser: true, building: false }));
vi.mock('$app/navigation', async () => {
    const { shallowRouting } = await import('../shallow-routing.test-fake.svelte');
    return { afterNavigate: shallowRouting.afterNavigate, beforeNavigate: shallowRouting.beforeNavigate, goto: shallowRouting.goto };
});
vi.mock('$app/state', async () => {
    const { shallowRouting } = await import('../shallow-routing.test-fake.svelte');
    return { navigating: shallowRouting.navigating, page: shallowRouting.page };
});

const { goto } = shallowRouting;
const detailSheetHistoryStateKey = '__exceptionlessDetailSheet';
const queryHistoryEntryIdKey = '__exceptionlessQueryHistoryEntryId';
const queryHistoryState = () => expect.objectContaining({ [queryHistoryEntryIdKey]: expect.any(String) });

async function click(name: string, container: ReturnType<typeof within> = screen) {
    await fireEvent.click(container.getByRole('button', { name }));
    await shallowRouting.flush();
}

function expectHistoryWrite(call: number, url: string, { replace = false, state = expect.anything() }: { replace?: boolean; state?: unknown } = {}) {
    expect(goto).toHaveBeenNthCalledWith(call, url, { persistState: true, replace, shallow: true, state });
}

function getEntryId(call: number): unknown {
    return (goto.mock.calls[call - 1]?.[1]?.state as Record<string, unknown> | undefined)?.[queryHistoryEntryIdKey];
}

async function settleCoalescingWindow() {
    await vi.advanceTimersByTimeAsync(200);
    await shallowRouting.flush();
}

describe('createQueryParameters', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        sessionStorage.clear();
        shallowRouting.reset('/');
    });

    afterEach(() => {
        vi.restoreAllMocks();
        vi.useRealTimers();
    });

    it('creates a durable history entry immediately while throttling rapid replacements', async () => {
        // Arrange
        render(QueryParametersTestHarness);

        // Act
        await click('First');
        await click('Second');

        // Assert
        expect(screen.getByText('second').textContent).toBe('second');
        expect(goto).toHaveBeenCalledOnce();
        expectHistoryWrite(1, '/?filter=first', { state: queryHistoryState() });
        expect(window.location.search).toBe('?filter=first');

        await settleCoalescingWindow();

        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(2, '/?filter=second', { replace: true, state: queryHistoryState() });
        expect(getEntryId(2)).toBe(getEntryId(1));
        expect(window.location.search).toBe('?filter=second');
    });

    it('allows hydration updates to replace even when user edits use push history', async () => {
        render(QueryParametersTestHarness);

        await click('Hydrate');

        expect(goto).toHaveBeenCalledOnce();
        expectHistoryWrite(1, '/?filter=hydrated', { replace: true, state: {} });
    });

    it('replaces a detail sheet entry when a filter adds push-history state', async () => {
        // Arrange
        shallowRouting.reset('/events?filter=open', { [detailSheetHistoryStateKey]: { key: 'event', value: 'abc123' } });
        render(QueryParametersTestHarness);

        // Act
        await click('First');

        // Assert
        expect(goto).toHaveBeenCalledOnce();
        expectHistoryWrite(1, '/events?filter=first', { replace: true, state: queryHistoryState() });
        expect(shallowRouting.historyEntryState()).not.toHaveProperty(detailSheetHistoryStateKey);
        expect(window.location.pathname + window.location.search).toBe('/events?filter=first');
    });

    it('pushes query history when the detail sheet marker is cleared', async () => {
        // Arrange
        shallowRouting.reset('/', { [detailSheetHistoryStateKey]: undefined });
        render(QueryParametersTestHarness);

        // Act
        await click('First');

        // Assert
        expect(goto).toHaveBeenCalledOnce();
        expectHistoryWrite(1, '/?filter=first', { state: queryHistoryState() });
    });

    it('cancels a pending replacement when state returns to the visible URL', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await click('First');
        await click('Second');

        // Act
        await click('First');
        await settleCoalescingWindow();

        // Assert
        expect(goto).toHaveBeenCalledOnce();
        expectHistoryWrite(1, '/?filter=first', { state: queryHistoryState() });
        expect(window.location.search).toBe('?filter=first');
    });

    it('starts a new push history entry after the coalescing window settles', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await click('First');
        await settleCoalescingWindow();

        // Act
        await click('Second');
        await settleCoalescingWindow();

        // Assert
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(1, '/?filter=first', { state: queryHistoryState() });
        expectHistoryWrite(2, '/?filter=second', { state: queryHistoryState() });
        expect(getEntryId(2)).not.toBe(getEntryId(1));
    });

    it('writes replace-history updates immediately without adding entries', async () => {
        // Arrange
        render(QueryParametersTestHarness, { history: 'replace' });

        // Act
        await click('First');
        await click('Second');

        // Assert
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(1, '/?filter=first', { replace: true, state: {} });
        expectHistoryWrite(2, '/?filter=second', { replace: true, state: {} });
        expect(window.location.search).toBe('?filter=second');
    });

    it('keeps a meaningful Back target when a burst returns to its starting URL', async () => {
        // Arrange
        shallowRouting.reset('/events#details');
        render(QueryParametersTestHarness);
        await click('First');

        // Act
        await click('Clear');
        await settleCoalescingWindow();

        // Assert
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(1, '/events?filter=first#details', { state: queryHistoryState() });
        expectHistoryWrite(2, '/events#details');
        expect(window.location.pathname).toBe('/events');
        expect(window.location.search).toBe('');
        expect(window.location.hash).toBe('#details');
    });

    it('starts a new coalescing burst after returning to the starting URL', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await click('First');
        await click('Clear');

        // Act
        await click('Second');

        // Assert
        expect(goto).toHaveBeenCalledTimes(3);
        expectHistoryWrite(1, '/?filter=first', { state: queryHistoryState() });
        expectHistoryWrite(2, '/');
        expectHistoryWrite(3, '/?filter=second', { state: queryHistoryState() });
        expect(getEntryId(3)).not.toBe(getEntryId(1));
    });

    it('recognizes an encoded equivalent of the starting URL', async () => {
        // Arrange
        shallowRouting.reset('/?filter=a%20b');
        render(QueryParametersTestHarness);
        await click('First');

        // Act
        await click('Spaced');

        // Assert
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(1, '/?filter=first', { state: queryHistoryState() });
        expectHistoryWrite(2, '/?filter=a+b');
    });

    it('recognizes an equivalent starting URL with reordered parameters', async () => {
        // Arrange
        shallowRouting.reset('/?project=p');
        render(QueryParametersTestHarness);
        await click('Alpha');
        await settleCoalescingWindow();
        await click('Clear');

        // Act
        await click('Alpha');

        // Assert
        expect(goto).toHaveBeenCalledTimes(3);
        expectHistoryWrite(2, '/?project=p', { state: queryHistoryState() });
        expectHistoryWrite(3, '/?project=p&filter=a');
    });

    it('ignores its own shallow navigations in navigation hooks', async () => {
        // Arrange: each history write runs the navigation hooks with `shallow: true`.
        render(QueryParametersTestHarness);
        await click('First');

        // Act
        await click('Second');
        await click('Alpha');
        await settleCoalescingWindow();

        // Assert: the burst still coalesces into one entry.
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(2, '/?filter=a', { replace: true, state: queryHistoryState() });
        expect(getEntryId(2)).toBe(getEntryId(1));
    });

    it('does not write history during a client-side navigation away from the page', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await click('First');
        await click('Second');

        // Act: a shallow write now would abort the navigation that is starting.
        shallowRouting.beforeLeaving('link');
        await settleCoalescingWindow();

        // Assert
        expect(goto).toHaveBeenCalledOnce();
        expect(window.location.search).toBe('?filter=first');
        expect(sessionStorage).toHaveLength(1);
    });

    it('keeps values written while a navigation renders the page', async () => {
        // Arrange: shallow history writes wait for the navigation that renders this page.
        render(QueryParametersTestHarness);
        const finishNavigation = shallowRouting.startNavigation();

        // Act: hydrate a value while the page renders, then let the navigation finish.
        await click('Hydrate');
        shallowRouting.afterNavigating('link');
        await finishNavigation();

        // Assert
        expectHistoryWrite(1, '/?filter=hydrated', { replace: true, state: {} });
        expect(window.location.search).toBe('?filter=hydrated');
        expect(screen.getByText('hydrated').textContent).toBe('hydrated');
    });

    it('flushes a throttled replacement before reload', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await click('First');
        await click('Second');

        // Act
        window.dispatchEvent(new Event('beforeunload'));
        await shallowRouting.flush();

        // Assert
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(2, '/?filter=second', { replace: true, state: queryHistoryState() });
        expect(window.location.search).toBe('?filter=second');
    });

    it('restores popstate without scheduling another history write', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await click('First');

        // Act
        await shallowRouting.traverse('/?filter=previous');
        await settleCoalescingWindow();

        // Assert
        expect(goto).toHaveBeenCalledOnce();
        expect(window.location.search).toBe('?filter=previous');
        expect(screen.getByText('previous').textContent).toBe('previous');
    });

    it('preserves a throttled replacement across immediate Back and Forward', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await click('First');
        await click('Second');
        const sourceState = shallowRouting.page.state;

        // Act: traverse Back before the replacement settles.
        await shallowRouting.traverse('/');

        // Assert: the destination is untouched and the pending source value is retained.
        expect(goto).toHaveBeenCalledOnce();
        expect(window.location.search).toBe('');
        expect(screen.getByRole('status').textContent).toBe('');

        // Act: traverse Forward to the source entry.
        await shallowRouting.traverse('/?filter=first', sourceState);

        // Assert: the source entry and reactive state restore the latest value.
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(2, '/?filter=second', { replace: true, state: queryHistoryState() });
        expect(window.location.search).toBe('?filter=second');
        expect(screen.getByText('second').textContent).toBe('second');
    });

    it('settles the source entry after Back and Forward without a pending replacement', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await click('First');
        const sourceState = shallowRouting.page.state;

        // Act: traverse away from and back to the source before its timer settles.
        await shallowRouting.traverse('/');
        await shallowRouting.traverse('/?filter=first', sourceState);
        await click('Second');

        // Assert: the later edit starts a new burst instead of replacing the old entry.
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(2, '/?filter=second', { state: queryHistoryState() });
        expect(getEntryId(2)).not.toBe(getEntryId(1));
    });

    it('falls back to in-memory coalescing when session storage is unavailable', async () => {
        // Arrange
        shallowRouting.reset('/', { [queryHistoryEntryIdKey]: 'source-entry' });
        const storageError = new DOMException('Storage is unavailable', 'SecurityError');
        vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
            throw storageError;
        });
        vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
            throw storageError;
        });
        vi.spyOn(Storage.prototype, 'removeItem').mockImplementation(() => {
            throw storageError;
        });
        render(QueryParametersTestHarness);

        // Act
        await click('First');
        await click('Second');
        await settleCoalescingWindow();

        // Assert
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(2, '/?filter=second', { replace: true, state: queryHistoryState() });
        expect(screen.getByText('second').textContent).toBe('second');
    });

    it('starts a new burst when the Back destination is edited', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await click('First');
        await click('Second');
        await shallowRouting.traverse('/');

        // Act
        await click('Alpha');
        await settleCoalescingWindow();

        // Assert
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(2, '/?filter=a', { state: queryHistoryState() });
        expect(getEntryId(2)).not.toBe(getEntryId(1));
        expect(window.location.search).toBe('?filter=a');
        expect(screen.getByText('a').textContent).toBe('a');
    });

    it('preserves a throttled replacement across route teardown', async () => {
        // Arrange
        const view = render(QueryParametersTestHarness);
        await click('First');
        await click('Second');
        const sourceState = shallowRouting.page.state;
        await shallowRouting.traverse('/');
        window.dispatchEvent(new Event('beforeunload'));
        expect(sessionStorage).toHaveLength(1);

        // Act: reload or leave the route, then recreate it by traversing Forward to the source entry.
        view.unmount();
        shallowRouting.visit('/?filter=first', sourceState);
        render(QueryParametersTestHarness);
        await shallowRouting.flush();

        // Assert
        expect(goto).toHaveBeenCalledTimes(2);
        expectHistoryWrite(2, '/?filter=second', { replace: true, state: queryHistoryState() });
        expect(getEntryId(2)).toBe(getEntryId(1));
        expect(window.location.search).toBe('?filter=second');
        expect(screen.getByText('second').textContent).toBe('second');
    });

    it('does not restore a retained replacement after its Forward branch is discarded', async () => {
        // Arrange
        const view = render(QueryParametersTestHarness);
        await click('First');
        await click('Second');
        await shallowRouting.traverse('/');

        // Act: link navigation discards Forward, then a later visit reuses the same URL.
        shallowRouting.beforeLeaving('link');
        view.unmount();
        shallowRouting.visit('/?filter=first');
        render(QueryParametersTestHarness);
        await shallowRouting.flush();

        // Assert
        expect(sessionStorage).toHaveLength(0);
        expect(goto).toHaveBeenCalledOnce();
        expect(window.location.search).toBe('?filter=first');
        expect(screen.getByText('first').textContent).toBe('first');
    });

    it('reads the visible URL when created after a shallow write left page.url stale', () => {
        // Arrange
        shallowRouting.reset('/?filter=visible');
        shallowRouting.page.url = new URL('/?filter=stale', window.location.origin);

        // Act
        render(QueryParametersTestHarness);

        // Assert
        expect(screen.getByText('visible').textContent).toBe('visible');
    });

    it('applies each instance update onto the live URL without overwriting other instances', async () => {
        // Arrange
        const filters = within(render(QueryParametersTestHarness).container);
        const projects = within(render(QueryParametersTestHarness, { history: 'replace', parameter: 'project' }).container);
        await click('First', filters);
        await settleCoalescingWindow();

        // Act
        await click('Alpha', projects);
        await click('Second', filters);
        await click('Clear', projects);

        // Assert
        expectHistoryWrite(2, '/?filter=first&project=a', { replace: true });
        expectHistoryWrite(3, '/?filter=second&project=a', { state: queryHistoryState() });
        expect(window.location.search).toBe('?filter=second');
        expect(filters.getByRole('status').textContent).toBe('second');
        expect(projects.getByRole('status').textContent).toBe('');
    });

    it('restores reactive state when shallow history is traversed', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await click('First');
        await settleCoalescingWindow();
        const firstState = shallowRouting.page.state;
        await click('Second');
        await settleCoalescingWindow();

        // Act
        await shallowRouting.traverse('/?filter=first', firstState);

        // Assert
        expect(goto).toHaveBeenCalledTimes(2);
        expect(screen.getByText('first').textContent).toBe('first');
    });
});
