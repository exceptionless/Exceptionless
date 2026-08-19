import { fireEvent, render, screen } from '@testing-library/svelte';
import { tick } from 'svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import QueryParametersTestHarness from './query-params.test-harness.svelte';

const navigation = vi.hoisted(() => ({
    afterNavigate: vi.fn(),
    beforeNavigate: vi.fn(),
    pushState: vi.fn(),
    replaceState: vi.fn()
}));
const pageState = vi.hoisted(() => ({}));
const detailSheetHistoryStateKey = '__exceptionlessDetailSheet';
const queryHistoryEntryIdKey = '__exceptionlessQueryHistoryEntryId';
const svelteKitPageStateKey = 'sveltekit:states';
const queryHistoryState = () => expect.objectContaining({ [queryHistoryEntryIdKey]: expect.any(String) });
const createSvelteKitHistoryState = (state: App.PageState) => ({ [svelteKitPageStateKey]: state });

vi.mock('$app/environment', () => ({ browser: true, building: false }));
vi.mock('$app/navigation', () => navigation);
vi.mock('$app/state', () => ({
    page: {
        state: pageState,
        get url() {
            return new URL(window.location.href);
        }
    }
}));

describe('createQueryParameters', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        vi.clearAllMocks();
        delete (pageState as Record<string, unknown>)[detailSheetHistoryStateKey];
        delete (pageState as Record<string, unknown>)[queryHistoryEntryIdKey];
        sessionStorage.clear();
        window.history.replaceState(createSvelteKitHistoryState(pageState), '', '/');
        navigation.pushState.mockImplementation((url: string | URL, state: App.PageState) =>
            window.history.pushState(createSvelteKitHistoryState(state), '', url)
        );
        navigation.replaceState.mockImplementation((url: string | URL, state: App.PageState) =>
            window.history.replaceState(createSvelteKitHistoryState(state), '', url)
        );
    });

    afterEach(() => {
        vi.restoreAllMocks();
        vi.useRealTimers();
    });

    it('creates a durable history entry immediately while throttling rapid replacements', async () => {
        // Arrange
        render(QueryParametersTestHarness);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));

        // Assert
        expect(screen.getByText('second').textContent).toBe('second');
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.pushState).toHaveBeenCalledWith('/?filter=first', queryHistoryState());
        expect(navigation.replaceState).not.toHaveBeenCalled();
        expect(window.location.search).toBe('?filter=first');

        await vi.advanceTimersByTimeAsync(200);

        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledWith('/?filter=second', queryHistoryState());
        expect(window.location.search).toBe('?filter=second');
    });

    it('replaces a detail sheet entry when a filter adds push-history state', async () => {
        // Arrange
        const detailEntry = { key: 'event', value: 'abc123' };
        Object.assign(pageState, { [detailSheetHistoryStateKey]: detailEntry });
        window.history.replaceState(createSvelteKitHistoryState({}), '', '/events?filter=open');
        window.history.pushState(createSvelteKitHistoryState(pageState), '', '/events?filter=open');
        render(QueryParametersTestHarness);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));

        // Assert
        expect(navigation.pushState).not.toHaveBeenCalled();
        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledWith('/events?filter=first', queryHistoryState());
        expect(window.history.state[svelteKitPageStateKey][detailSheetHistoryStateKey]).toBeUndefined();
        expect(window.location.pathname + window.location.search).toBe('/events?filter=first');
    });

    it('pushes query history when the detail sheet marker is cleared', async () => {
        // Arrange
        Object.assign(pageState, { [detailSheetHistoryStateKey]: undefined });
        render(QueryParametersTestHarness);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));

        // Assert
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).not.toHaveBeenCalled();
    });

    it('cancels a pending replacement when state returns to the visible URL', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.pushState).toHaveBeenCalledWith('/?filter=first', queryHistoryState());
        expect(navigation.replaceState).not.toHaveBeenCalled();
        expect(window.location.search).toBe('?filter=first');
    });

    it('starts a new push history entry after the coalescing window settles', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await vi.advanceTimersByTimeAsync(200);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(navigation.pushState).toHaveBeenCalledTimes(2);
        expect(navigation.pushState).toHaveBeenNthCalledWith(1, '/?filter=first', queryHistoryState());
        expect(navigation.pushState).toHaveBeenNthCalledWith(2, '/?filter=second', queryHistoryState());
        expect(navigation.replaceState).not.toHaveBeenCalled();
    });

    it('writes replace-history updates immediately without adding entries', async () => {
        // Arrange
        render(QueryParametersTestHarness, { history: 'replace' });

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));

        // Assert
        expect(navigation.pushState).not.toHaveBeenCalled();
        expect(navigation.replaceState).toHaveBeenCalledTimes(2);
        expect(navigation.replaceState).toHaveBeenNthCalledWith(1, '/?filter=first', pageState);
        expect(navigation.replaceState).toHaveBeenNthCalledWith(2, '/?filter=second', pageState);
        expect(window.location.search).toBe('?filter=second');
    });

    it('allows a push-history query to replace its current entry explicitly', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Replace' }));
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledWith('/?filter=replacement', pageState);
        expect(window.location.search).toBe('?filter=replacement');
    });

    it('keeps a meaningful Back target when a burst returns to its starting URL', async () => {
        // Arrange
        window.history.replaceState({}, '', '/events#details');
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Clear' }));
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(navigation.pushState).toHaveBeenCalledTimes(2);
        expect(navigation.pushState).toHaveBeenNthCalledWith(1, '/events?filter=first#details', queryHistoryState());
        expect(navigation.pushState).toHaveBeenNthCalledWith(2, '/events#details', pageState);
        expect(navigation.replaceState).not.toHaveBeenCalled();
        expect(window.location.pathname).toBe('/events');
        expect(window.location.search).toBe('');
        expect(window.location.hash).toBe('#details');
    });

    it('starts a new coalescing burst after returning to the starting URL', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Clear' }));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));

        // Assert
        expect(navigation.pushState).toHaveBeenCalledTimes(3);
        expect(navigation.pushState).toHaveBeenNthCalledWith(1, '/?filter=first', queryHistoryState());
        expect(navigation.pushState).toHaveBeenNthCalledWith(2, '/', pageState);
        expect(navigation.pushState).toHaveBeenNthCalledWith(3, '/?filter=second', queryHistoryState());
        expect(navigation.replaceState).not.toHaveBeenCalled();
    });

    it('recognizes an encoded equivalent of the starting URL', async () => {
        // Arrange
        window.history.replaceState({}, '', '/?filter=a%20b');
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Spaced' }));

        // Assert
        expect(navigation.pushState).toHaveBeenCalledTimes(2);
        expect(navigation.pushState).toHaveBeenNthCalledWith(1, '/?filter=first', queryHistoryState());
        expect(navigation.pushState).toHaveBeenNthCalledWith(2, '/?filter=a+b', pageState);
        expect(navigation.replaceState).not.toHaveBeenCalled();
    });

    it('recognizes an equivalent starting URL with reordered parameters', async () => {
        // Arrange
        window.history.replaceState({}, '', '/?project=p');
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'Alpha' }));
        await vi.advanceTimersByTimeAsync(200);
        await fireEvent.click(screen.getByRole('button', { name: 'Clear' }));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Alpha' }));

        // Assert
        expect(navigation.pushState).toHaveBeenCalledTimes(3);
        expect(navigation.pushState).toHaveBeenNthCalledWith(2, '/?project=p', queryHistoryState());
        expect(navigation.pushState).toHaveBeenNthCalledWith(3, '/?project=p&filter=a', pageState);
        expect(navigation.replaceState).not.toHaveBeenCalled();
    });

    it('does not add a delayed history write after full navigation', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        const beforeNavigation = navigation.beforeNavigate.mock.calls[0]?.[0] as ((navigation: { type: string }) => void) | undefined;

        // Act
        beforeNavigation?.({ type: 'link' });
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(beforeNavigation).toBeDefined();
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.pushState).toHaveBeenCalledWith('/?filter=first', queryHistoryState());
        expect(navigation.replaceState).not.toHaveBeenCalled();
        expect(window.location.search).toBe('?filter=first');
    });

    it('flushes a throttled replacement before reload', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));

        // Act
        window.dispatchEvent(new Event('beforeunload'));

        // Assert
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledWith('/?filter=second', queryHistoryState());
        expect(window.location.search).toBe('?filter=second');
    });

    it('restores popstate without scheduling another history write', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        const beforeNavigation = navigation.beforeNavigate.mock.calls[0]?.[0] as ((navigation: { type: string }) => void) | undefined;

        // Act
        window.history.replaceState(pageState, '', '?filter=previous');
        beforeNavigation?.({ type: 'popstate' });
        window.dispatchEvent(new PopStateEvent('popstate', { state: pageState }));
        await vi.advanceTimersByTimeAsync(200);
        await tick();

        // Assert
        expect(beforeNavigation).toBeDefined();
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).not.toHaveBeenCalled();
        expect(window.location.search).toBe('?filter=previous');
        expect(screen.getByText('previous').textContent).toBe('previous');
    });

    it('preserves a throttled replacement across immediate Back and Forward', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));
        const sourceEntryState = window.history.state;
        const sourcePageState = navigation.pushState.mock.calls[0]?.[1] as Record<string, unknown>;
        (pageState as Record<string, unknown>)[queryHistoryEntryIdKey] = sourcePageState[queryHistoryEntryIdKey];

        // Act: traverse Back before the replacement settles while page.state is still stale.
        window.history.replaceState({}, '', '/');
        window.dispatchEvent(new PopStateEvent('popstate', { state: pageState }));
        await tick();

        // Assert: the destination is untouched and the pending source value is retained.
        expect(navigation.replaceState).not.toHaveBeenCalled();
        expect(window.location.search).toBe('');
        expect(document.querySelector('output')?.textContent).toBe('');

        // Act: traverse Forward to the source entry.
        window.history.replaceState(sourceEntryState, '', '/?filter=first');
        window.dispatchEvent(new PopStateEvent('popstate', { state: sourceEntryState }));
        await tick();

        // Assert: the source entry and reactive state restore the latest value.
        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledWith('/?filter=second', queryHistoryState());
        expect(window.location.search).toBe('?filter=second');
        expect(screen.getByText('second').textContent).toBe('second');
    });

    it('settles the source entry after Back and Forward without a pending replacement', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        const sourceEntryState = window.history.state;

        // Act: traverse away from and back to the source before its timer settles.
        window.history.replaceState({}, '', '/');
        window.dispatchEvent(new PopStateEvent('popstate', { state: pageState }));
        await tick();
        window.history.replaceState(sourceEntryState, '', '/?filter=first');
        window.dispatchEvent(new PopStateEvent('popstate', { state: sourceEntryState }));
        await tick();
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));

        // Assert: the later edit starts a new burst instead of replacing the old entry.
        expect(navigation.pushState).toHaveBeenCalledTimes(2);
        expect(navigation.pushState).toHaveBeenNthCalledWith(2, '/?filter=second', queryHistoryState());
        expect(navigation.replaceState).not.toHaveBeenCalled();
    });

    it('falls back to in-memory coalescing when session storage is unavailable', async () => {
        // Arrange
        window.history.replaceState(createSvelteKitHistoryState({ [queryHistoryEntryIdKey]: 'source-entry' }), '', '/');
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
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledWith('/?filter=second', queryHistoryState());
        expect(screen.getByText('second').textContent).toBe('second');
    });

    it('starts a new burst when the Back destination is edited', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));
        const beforeNavigation = navigation.beforeNavigate.mock.calls[0]?.[0] as ((navigation: { type: string }) => void) | undefined;
        window.history.replaceState(pageState, '', '/');
        beforeNavigation?.({ type: 'popstate' });
        window.dispatchEvent(new PopStateEvent('popstate', { state: pageState }));
        await tick();

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Alpha' }));
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(navigation.pushState).toHaveBeenCalledTimes(2);
        expect(navigation.pushState).toHaveBeenNthCalledWith(2, '/?filter=a', queryHistoryState());
        expect(navigation.replaceState).not.toHaveBeenCalled();
        expect(window.location.search).toBe('?filter=a');
        expect(screen.getByText('a').textContent).toBe('a');
    });

    it('preserves a throttled replacement across route teardown', async () => {
        // Arrange
        const view = render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));
        const sourceEntryState = window.history.state;
        const beforeNavigation = navigation.beforeNavigate.mock.calls[0]?.[0] as ((navigation: { type: string }) => void) | undefined;
        window.history.replaceState(pageState, '', '/');
        beforeNavigation?.({ type: 'popstate' });
        window.dispatchEvent(new PopStateEvent('popstate', { state: pageState }));
        await tick();
        window.dispatchEvent(new Event('beforeunload'));
        expect(sessionStorage).toHaveLength(1);

        // Act: reload/leave the route, then recreate it by traversing Forward to the source entry.
        view.unmount();
        window.history.replaceState(sourceEntryState, '', '/?filter=first');
        render(QueryParametersTestHarness);
        await tick();

        // Assert
        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledWith('/?filter=second', queryHistoryState());
        expect(window.location.search).toBe('?filter=second');
        expect(screen.getByText('second').textContent).toBe('second');
    });

    it('does not restore a retained replacement after its Forward branch is discarded', async () => {
        // Arrange
        const view = render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));
        const beforeNavigation = navigation.beforeNavigate.mock.calls[0]?.[0] as ((navigation: { type: string }) => void) | undefined;
        window.history.replaceState(pageState, '', '/');
        beforeNavigation?.({ type: 'popstate' });
        window.dispatchEvent(new PopStateEvent('popstate', { state: pageState }));
        await tick();

        // Act: link navigation discards Forward, then a later visit reuses the same URL.
        beforeNavigation?.({ type: 'link' });
        view.unmount();
        window.history.replaceState(pageState, '', '/?filter=first');
        render(QueryParametersTestHarness);
        await tick();

        // Assert
        expect(sessionStorage).toHaveLength(0);
        expect(navigation.replaceState).not.toHaveBeenCalled();
        expect(window.location.search).toBe('?filter=first');
        expect(screen.getByText('first').textContent).toBe('first');
    });

    it('restores reactive state when shallow history is traversed', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await vi.advanceTimersByTimeAsync(200);
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));
        await vi.advanceTimersByTimeAsync(200);

        // Act
        window.history.replaceState(pageState, '', '?filter=first');
        window.dispatchEvent(new PopStateEvent('popstate', { state: pageState }));
        await tick();

        // Assert
        expect(navigation.pushState).toHaveBeenCalledTimes(2);
        expect(navigation.replaceState).not.toHaveBeenCalled();
        expect(screen.getByText('first').textContent).toBe('first');
    });
});
