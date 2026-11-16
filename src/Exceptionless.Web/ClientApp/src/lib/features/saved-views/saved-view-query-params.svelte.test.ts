import { fireEvent, render, screen } from '@testing-library/svelte';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { shallowRouting } from '#shared/shallow-routing.test-fake.svelte.ts';

import SavedViewQueryParametersTestHarness from './saved-view-query-params.test-harness.svelte';

vi.mock('$app/env', () => ({ browser: true, building: false }));
vi.mock('$app/navigation', async () => {
    const { shallowRouting } = await import('#shared/shallow-routing.test-fake.svelte.ts');
    return { afterNavigate: shallowRouting.afterNavigate, beforeNavigate: shallowRouting.beforeNavigate, goto: shallowRouting.goto };
});
vi.mock('$app/state', async () => {
    const { shallowRouting } = await import('#shared/shallow-routing.test-fake.svelte.ts');
    return { navigating: shallowRouting.navigating, page: shallowRouting.page };
});
vi.mock('#features/auth/index.svelte.ts', () => ({
    accessToken: { current: 'token_123' }
}));

describe('saved view query parameter clearing', () => {
    beforeEach(() => {
        vi.useFakeTimers();
        sessionStorage.clear();
        shallowRouting.reset('/stream?saved=view-1&filter=environment%3Aproduction&filters=serialized&limit=50&project=project-1');
    });

    afterEach(() => {
        vi.restoreAllMocks();
        vi.useRealTimers();
    });

    it.each([
        { action: 'Clear deleted view', historyEntriesAdded: 0, reason: 'replaces the entry for a view deleted elsewhere' },
        { action: 'Clear view', historyEntriesAdded: 1, reason: 'preserves Back history when the user clears a view' }
    ])('$reason', async ({ action, historyEntriesAdded }) => {
        // Arrange
        render(SavedViewQueryParametersTestHarness);
        const historyLength = window.history.length;

        // Act
        await fireEvent.click(screen.getByRole('button', { name: action }));
        await shallowRouting.flush();
        await vi.advanceTimersByTimeAsync(200);
        await shallowRouting.flush();

        // Assert
        expect(window.location.pathname + window.location.search).toBe('/stream?limit=50&project=project-1');
        expect(window.history.length).toBe(historyLength + historyEntriesAdded);
        expect(screen.getByRole('status')).toHaveTextContent('No saved view');
    });
});
