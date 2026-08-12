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
const queryHistoryState = () => expect.objectContaining({ __exceptionlessQueryHistoryEntryId: expect.any(String) });

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
        sessionStorage.clear();
        window.history.replaceState({}, '', '/');
        navigation.pushState.mockImplementation((url: string | URL, state: App.PageState) => window.history.pushState(state, '', url));
        navigation.replaceState.mockImplementation((url: string | URL, state: App.PageState) => window.history.replaceState(state, '', url));
    });

    afterEach(() => {
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
        const beforeNavigation = navigation.beforeNavigate.mock.calls[0]?.[0] as ((navigation: { type: string }) => void) | undefined;

        // Act: traverse Back before the replacement settles.
        window.history.replaceState(pageState, '', '/');
        beforeNavigation?.({ type: 'popstate' });
        window.dispatchEvent(new PopStateEvent('popstate', { state: pageState }));
        await tick();

        // Assert: the destination is untouched and the pending source value is retained.
        expect(navigation.replaceState).not.toHaveBeenCalled();
        expect(window.location.search).toBe('');
        expect(document.querySelector('output')?.textContent).toBe('');

        // Act: traverse Forward to the source entry.
        window.history.replaceState(pageState, '', '/?filter=first');
        beforeNavigation?.({ type: 'popstate' });
        window.dispatchEvent(new PopStateEvent('popstate', { state: pageState }));
        await tick();

        // Assert: the source entry and reactive state restore the latest value.
        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledWith('/?filter=second', queryHistoryState());
        expect(window.location.search).toBe('?filter=second');
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
        const sourceEntryState = navigation.pushState.mock.calls[0]?.[1] as App.PageState;
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
        expect(navigation.replaceState).toHaveBeenCalledWith('/?filter=second', sourceEntryState);
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
