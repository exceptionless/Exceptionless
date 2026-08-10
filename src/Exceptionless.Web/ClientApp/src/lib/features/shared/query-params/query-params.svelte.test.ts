import { fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import QueryParametersTestHarness from './query-params.test-harness.svelte';

const navigation = vi.hoisted(() => ({
    afterNavigate: vi.fn(),
    beforeNavigate: vi.fn(),
    pushState: vi.fn(),
    replaceState: vi.fn()
}));
const pageState = vi.hoisted(() => ({}));

vi.mock('$app/environment', () => ({ building: false }));
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
        window.history.replaceState({}, '', '/');
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
        expect(navigation.replaceState).toHaveBeenCalledOnce();
        expect(navigation.replaceState).toHaveBeenCalledWith('?filter=second', pageState);
        expect(navigation.pushState).not.toHaveBeenCalled();
    });
});
