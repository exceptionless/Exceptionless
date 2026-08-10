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

    it('writes only the latest debounced update with shallow routing', async () => {
        // Arrange
        render(QueryParametersTestHarness);

        // Act
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        await fireEvent.click(screen.getByRole('button', { name: 'Second' }));
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(screen.getByText('second').textContent).toBe('second');
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.pushState).toHaveBeenCalledWith('?filter=second', pageState);
        expect(navigation.replaceState).not.toHaveBeenCalled();
    });

    it('flushes a pending update before full navigation', async () => {
        // Arrange
        render(QueryParametersTestHarness);
        await fireEvent.click(screen.getByRole('button', { name: 'First' }));
        const beforeNavigation = navigation.beforeNavigate.mock.calls[0]?.[0] as (() => void) | undefined;

        // Act
        beforeNavigation?.();
        await vi.advanceTimersByTimeAsync(200);

        // Assert
        expect(beforeNavigation).toBeDefined();
        expect(navigation.pushState).toHaveBeenCalledOnce();
        expect(navigation.pushState).toHaveBeenCalledWith('?filter=first', pageState);
        expect(window.location.search).toBe('?filter=first');
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
        expect(screen.getByText('first').textContent).toBe('first');
    });
});
