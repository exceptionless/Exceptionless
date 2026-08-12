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

vi.mock('$app/environment', () => ({ browser: true, building: false }));
vi.mock('$app/navigation', () => navigation);
vi.mock('$app/state', () => ({
    page: {
        state: pageState,
        url: new URL('http://localhost/')
    }
}));

describe('createQueryParameters', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        vi.clearAllMocks();
        window.history.replaceState({}, '', '/');
        navigation.pushState.mockImplementation((url: string | URL, state: App.PageState) => window.history.pushState(state, '', url));
        navigation.replaceState.mockImplementation((url: string | URL, state: App.PageState) => window.history.replaceState(state, '', url));
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it('writes updates immediately and commits one settled push history entry', async () => {
        // Arrange
        render(QueryParametersTestHarness);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));

        // Assert
        expect(screen.getByText('second').textContent).toBe('second');
        expect(navigation.pushState).not.toHaveBeenCalled();
        expect(navigation.replaceState).toHaveBeenCalledTimes(2);
        expect(navigation.replaceState).toHaveBeenNthCalledWith(1, '/?filter=first', pageState);
        expect(navigation.replaceState).toHaveBeenNthCalledWith(2, '/?filter=second', pageState);
        expect(window.location.search).toBe('?filter=second');

        await vi.advanceTimersByTimeAsync(200);

        expect(navigation.replaceState).toHaveBeenNthCalledWith(3, '/', pageState);
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.pushState).toHaveBeenCalledWith('/?filter=second', pageState);
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
        expect(navigation.pushState).toHaveBeenNthCalledWith(1, '/?filter=first', pageState);
        expect(navigation.pushState).toHaveBeenNthCalledWith(2, '/?filter=second', pageState);
        expect(navigation.replaceState).toHaveBeenCalledTimes(4);
        expect(navigation.replaceState).toHaveBeenNthCalledWith(4, '/?filter=first', pageState);
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

    it('does not push a duplicate entry or trailing question mark when a burst returns to its starting URL', async () => {
        // Arrange
        window.history.replaceState({}, '', '/events#details');
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'Clear' }));
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(navigation.pushState).not.toHaveBeenCalled();
        expect(navigation.replaceState).toHaveBeenCalledTimes(2);
        expect(navigation.replaceState).toHaveBeenCalledWith('/events#details', pageState);
        expect(window.location.pathname).toBe('/events');
        expect(window.location.search).toBe('');
        expect(window.location.hash).toBe('#details');
    });

    it('commits a pending push history entry before full navigation', async () => {
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
        expect(navigation.pushState).toHaveBeenCalledWith('/?filter=first', pageState);
        expect(navigation.replaceState).toHaveBeenCalledTimes(2);
        expect(navigation.replaceState).toHaveBeenNthCalledWith(2, '/', pageState);
        expect(window.location.search).toBe('?filter=first');
    });

    it('restores popstate without scheduling another history write', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        const beforeNavigation = navigation.beforeNavigate.mock.calls[0]?.[0] as ((navigation: { type: string }) => void) | undefined;

        // Act
        beforeNavigation?.({ type: 'popstate' });
        window.history.replaceState(pageState, '', '?filter=previous');
        window.dispatchEvent(new PopStateEvent('popstate', { state: pageState }));
        await vi.advanceTimersByTimeAsync(200);
        await tick();

        // Assert
        expect(beforeNavigation).toBeDefined();
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledTimes(2);
        expect(window.location.search).toBe('?filter=previous');
        expect(screen.getByText('previous').textContent).toBe('previous');
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
        expect(navigation.replaceState).toHaveBeenCalledTimes(4);
        expect(screen.getByText('first').textContent).toBe('first');
    });
});
