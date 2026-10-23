import { beforeEach, describe, expect, it, vi } from 'vitest';

import { updateShallowHistory, waitForShallowHistory } from './history-state';
import { shallowRouting } from './shallow-routing.test-fake.svelte';

vi.mock('$app/navigation', async () => {
    const { shallowRouting } = await import('./shallow-routing.test-fake.svelte');
    return { goto: shallowRouting.goto };
});
vi.mock('$app/state', async () => {
    const { shallowRouting } = await import('./shallow-routing.test-fake.svelte');
    return { navigating: shallowRouting.navigating, page: shallowRouting.page };
});

const { goto, startNavigation } = shallowRouting;

describe('updateShallowHistory', () => {
    beforeEach(() => {
        shallowRouting.reset('/events');
    });

    it('waits for an in-flight navigation, because a shallow goto would abort it', async () => {
        // Arrange
        const finishNavigation = startNavigation();

        // Act
        const write = updateShallowHistory({ url: () => '/events?filter=open' });
        await shallowRouting.flush();

        // Assert
        expect(goto).not.toHaveBeenCalled();
        await finishNavigation();
        await write;
        expect(goto).toHaveBeenCalledExactlyOnceWith('/events?filter=open', { persistState: true, replace: false, shallow: true, state: {} });
    });

    it('drops a write when the page that queued it has been navigated away from', async () => {
        // Arrange
        const finishNavigation = startNavigation();
        const write = updateShallowHistory({ url: () => '/events?filter=open' });

        // Act
        shallowRouting.visit('/stacks');
        await finishNavigation();
        await write;

        // Assert
        expect(goto).not.toHaveBeenCalled();
        expect(window.location.pathname + window.location.search).toBe('/stacks');
    });

    it('derives each queued write from the entry current when it runs', async () => {
        // Act: two writers queue in the same task, before either write is applied.
        void updateShallowHistory({ replace: true, state: (state) => ({ ...state, first: true }) });
        void updateShallowHistory({ replace: true, state: (state) => ({ ...state, second: true }) });
        await waitForShallowHistory();

        // Assert
        expect(goto).toHaveBeenCalledTimes(2);
        expect(shallowRouting.historyEntryState()).toEqual({ first: true, second: true });
    });
});
